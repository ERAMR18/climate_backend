import { Body, Controller, Get, HttpCode, Param, ParseUUIDPipe, Patch, Post, Put, Query, Req, SetMetadata, UseGuards } from '@nestjs/common';
import { IsBooleanString, IsIn, IsOptional, IsString, MaxLength } from 'class-validator';
import { AuthGuard, AuthRequest } from './auth.guard';
import { Database } from './database';
import { roles } from './config';
import { CreateUserDto, LoginDto, RegisterDto, StatusDto, UpdateUserDto } from './dtos';
import { AuditContext, UsersService } from './users.service';

const context = (req: AuthRequest): AuditContext => ({ actor: req.identity, correlationId: req.correlationId, ipAddress: req.ip });
class SearchDto {
  @IsOptional() @IsString() @MaxLength(254) search?: string;
  @IsOptional() @IsIn(roles) role?: string;
  @IsOptional() @IsBooleanString() isActive?: string;
}
@Controller('api/v1/auth')
export class AuthController {
  constructor(private readonly users: UsersService) {}
  @Post('register') register(@Body() input: RegisterDto, @Req() req: AuthRequest) { return this.users.create(input, 'Viewer', context(req)); }
  @Post('login') @HttpCode(200) login(@Body() input: LoginDto, @Req() req: AuthRequest) { return this.users.login(input, context(req)); }
  @Post('logout') @HttpCode(204) @UseGuards(AuthGuard)
  logout(@Req() req: AuthRequest) { return this.users.logout(req.identity, context(req)); }
}
@Controller('api/v1/users') @UseGuards(AuthGuard)
export class UsersController {
  constructor(private readonly users: UsersService) {}
  @Get('me') me(@Req() req: AuthRequest) { return this.users.get(req.identity.sub); }
  @Get() @SetMetadata('admin', true) list(@Query() query: SearchDto) {
    return this.users.list(query.search, query.role, query.isActive === undefined ? undefined : query.isActive === 'true');
  }
  @Post() @SetMetadata('admin', true) create(@Body() input: CreateUserDto, @Req() req: AuthRequest) {
    return this.users.create(input, input.role, context(req));
  }
  @Get(':id') @SetMetadata('admin', true) get(@Param('id', new ParseUUIDPipe()) id: string) { return this.users.get(id); }
  @Put(':id') @SetMetadata('admin', true) update(@Param('id', new ParseUUIDPipe()) id: string, @Body() input: UpdateUserDto, @Req() req: AuthRequest) {
    return this.users.update(id, input, context(req));
  }
  @Patch(':id/status') @HttpCode(204) @SetMetadata('admin', true)
  status(@Param('id', new ParseUUIDPipe()) id: string, @Body() input: StatusDto, @Req() req: AuthRequest) { return this.users.status(id, input.isActive, context(req)); }
}
@Controller('health')
export class HealthController {
  constructor(private readonly database: Database) {}
  @Get('live') live() { return { status: 'Healthy' }; }
  @Get() async health() { await this.database.pool.query('SELECT 1'); return { status: 'Healthy' }; }
  @Get('ready') ready() { return this.health(); }
}
