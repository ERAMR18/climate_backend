import { Injectable, Logger, OnModuleDestroy, OnModuleInit } from '@nestjs/common';
import amqp, { ChannelModel, ConfirmChannel } from 'amqplib';
import { Database } from './database';
import { required } from './config';

@Injectable()
export class Outbox implements OnModuleInit, OnModuleDestroy {
  private readonly logger = new Logger(Outbox.name);
  private connection?: ChannelModel;
  private channel?: ConfirmChannel;
  private timer?: NodeJS.Timeout;
  private stopped = false;
  private running?: Promise<void>;
  private readonly url = required('RABBITMQ_URL');
  constructor(private readonly database: Database) {}
  onModuleInit(): void { this.schedule(0); }
  private schedule(delay: number): void {
    if (this.stopped) return;
    this.timer = setTimeout(() => {
      this.running = this.dispatch().catch(() => this.logger.warn('Audit delivery failed; unconfirmed events remain in PostgreSQL.'))
        .finally(() => this.schedule(5000));
    }, delay);
  }
  async dispatch(): Promise<void> {
    try {
      if (!this.channel) {
        this.connection = await amqp.connect(this.url, { timeout: 5000 });
        this.connection.on('error', () => this.logger.warn('RabbitMQ connection interrupted.'));
        this.connection.on('close', () => { this.channel = undefined; });
        this.channel = await this.connection.createConfirmChannel();
        this.channel.on('error', () => this.logger.warn('RabbitMQ channel interrupted.'));
        await this.channel.assertExchange('climate.audit', 'direct', { durable: true });
        await this.channel.assertExchange('climate.audit.dead', 'direct', { durable: true });
        await this.channel.assertQueue('climate.audit.events.dlq', { durable: true });
        await this.channel.bindQueue('climate.audit.events.dlq', 'climate.audit.dead', 'audit.created');
        await this.channel.assertQueue('climate.audit.events', { durable: true, arguments: {
          'x-dead-letter-exchange': 'climate.audit.dead', 'x-dead-letter-routing-key': 'audit.created' } });
        await this.channel.bindQueue('climate.audit.events', 'climate.audit', 'audit.created');
      }
      const channel = this.channel;
      await this.database.transaction(async client => {
        const messages = await client.query('SELECT * FROM audit_outbox WHERE published_at IS NULL ORDER BY created_at LIMIT 100 FOR UPDATE SKIP LOCKED');
        for (const row of messages.rows) {
          await new Promise<void>((resolve, reject) => {
            const timeout = setTimeout(() => reject(new Error('Publisher confirm timeout')), 10000);
            const returned = () => { clearTimeout(timeout); reject(new Error('Audit event unroutable')); };
            channel.once('return', returned);
            channel.publish('climate.audit', 'audit.created', Buffer.from(JSON.stringify(row.payload)), {
              persistent: true, mandatory: true, contentType: 'application/json', type: 'AuditLogRequested',
              messageId: row.id, correlationId: row.payload.CorrelationId }, error => {
              clearTimeout(timeout);
              channel.off('return', returned);
              if (error) reject(error); else resolve();
            });
          });
          await client.query('UPDATE audit_outbox SET published_at=now() WHERE id=$1', [row.id]);
        }
      });
      await this.database.pool.query('DELETE FROM revoked_tokens WHERE expires_at < now()');
    } catch (error) {
      this.channel = undefined;
      await this.connection?.close().catch(() => undefined);
      this.connection = undefined;
      throw error;
    }
  }
  async onModuleDestroy(): Promise<void> {
    this.stopped = true;
    clearTimeout(this.timer);
    await this.running;
    await this.connection?.close().catch(() => undefined);
  }
}
