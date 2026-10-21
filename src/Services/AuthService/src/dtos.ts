import { IsBoolean, IsEmail, IsIn, IsString, Length, Matches } from 'class-validator';
import { roles, Role } from './config';

export class RegisterDto {
  @IsString() @Length(3, 50) @Matches(/^[a-zA-Z0-9._-]+$/) username!: string;
  @IsEmail() @Length(3, 254) email!: string;
  @IsString() @Length(12, 128) @Matches(/[A-Z]/) @Matches(/[a-z]/) @Matches(/[0-9]/) password!: string;
}
export class CreateUserDto extends RegisterDto {
  @IsIn(roles) role!: Role;
}
export class LoginDto {
  @IsString() @Length(1, 254) login!: string;
  @IsString() @Length(1, 128) password!: string;
}
export class UpdateUserDto {
  @IsString() @Length(3, 50) @Matches(/^[a-zA-Z0-9._-]+$/) username!: string;
  @IsEmail() @Length(3, 254) email!: string;
  @IsIn(roles) role!: Role;
}
export class StatusDto { @IsBoolean() isActive!: boolean; }
