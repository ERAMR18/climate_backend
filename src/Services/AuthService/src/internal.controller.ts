import { Body, Controller, HttpCode, Post, Req, UnauthorizedException } from '@nestjs/common';
import { IsIn } from 'class-validator';
import { timingSafeEqual } from 'node:crypto';
import { Request } from 'express';
import { Tokens } from './tokens';

class ServiceTokenDto { @IsIn(['reports']) service!: string; }

@Controller('api/v1/internal/auth')
export class InternalAuthController {
  constructor(private readonly tokens: Tokens) {}
  @Post('service-token') @HttpCode(200)
  token(@Body() input: ServiceTokenDto, @Req() request: Request) {
    const expected = process.env.INTERNAL_API_KEY ?? process.env.InternalApi__ApiKey ?? '';
    const supplied = request.header('X-Internal-Api-Key') ?? '';
    if (expected.length < 32 || Buffer.byteLength(supplied) !== Buffer.byteLength(expected)
      || !timingSafeEqual(Buffer.from(expected), Buffer.from(supplied))) throw new UnauthorizedException();
    return this.tokens.issue({ id: 'eeeeeeee-eeee-4eee-8eee-eeeeeeeeeeee', username: 'report-service',
      email: 'report-service@internal.invalid', role: 'Viewer' }, 5);
  }
}
