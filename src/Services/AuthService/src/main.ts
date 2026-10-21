import 'reflect-metadata';
import { ArgumentsHost, Catch, ExceptionFilter, HttpException, Logger, Module, ValidationPipe } from '@nestjs/common';
import { NestFactory } from '@nestjs/core';
import { Request, Response, NextFunction } from 'express';
import { randomUUID } from 'node:crypto';
import { AuthGuard, AuthRequest } from './auth.guard';
import { AuthController, HealthController, UsersController } from './controllers';
import { Database } from './database';
import { Outbox } from './outbox';
import { Tokens } from './tokens';
import { UsersService } from './users.service';
import { InternalAuthController } from './internal.controller';

@Catch()
class Problems implements ExceptionFilter {
  private readonly logger = new Logger('Problems');
  catch(error: unknown, host: ArgumentsHost): void {
    const response = host.switchToHttp().getResponse<Response>();
    const request = host.switchToHttp().getRequest<AuthRequest>();
    let status = error instanceof HttpException ? error.getStatus() : 500;
    if ((error as { code?: string }).code === '23505') status = 409;
    if (status === 500) this.logger.error('Request failed', error instanceof Error ? error.name : 'UnknownError');
    const detail = error instanceof HttpException ? error.getResponse() : undefined;
    response.status(status).type('application/problem+json').json({ type: 'about:blank', status,
      title: status === 409 ? 'Username or email already registered.' : status === 500 ? 'Internal server error.' : error instanceof HttpException ? error.message : 'Request failed.',
      ...(typeof detail === 'object' ? { errors: detail } : {}), traceId: request.correlationId });
  }
}
@Module({ controllers: [AuthController, UsersController, HealthController, InternalAuthController], providers: [Database, Tokens, UsersService, Outbox, AuthGuard] })
class AppModule {}

async function bootstrap(): Promise<void> {
  const app = await NestFactory.create(AppModule);
  app.enableShutdownHooks();
  app.use((req: Request, res: Response, next: NextFunction) => {
    const incoming = req.header('X-Correlation-ID');
    const id = incoming && /^[a-zA-Z0-9._-]{1,128}$/.test(incoming) ? incoming : randomUUID();
    (req as AuthRequest).correlationId = id;
    res.setHeader('X-Correlation-ID', id);
    next();
  });
  app.useGlobalPipes(new ValidationPipe({ whitelist: true, forbidNonWhitelisted: true, transform: true, errorHttpStatusCode: 422 }));
  app.useGlobalFilters(new Problems());
  await app.listen(Number(process.env.PORT ?? 8080), '0.0.0.0');
}
bootstrap().catch(() => { new Logger('Bootstrap').error('Auth initialization failed; check configuration and PostgreSQL availability.'); process.exitCode = 1; });
