import type { DatabaseType, SystemEnvironment } from './common';

export interface DbConnectionDto {
  connectionId: string;
  connectionName: string;
  databaseType: DatabaseType;
  environmentType: SystemEnvironment;
  isActiveEnv: boolean;
  host: string;
  port: number;
  serviceName?: string;
  pdbName?: string;
  username: string;
  standbyHost?: string;
  standbyPort?: number;
  standbyService?: string;
  description?: string;
  testResult?: string;
  testedAt?: string;
  createdAt?: string;
  updatedAt?: string;
}

export interface DbConnectionUpsertDto {
  connectionName: string;
  databaseType: DatabaseType;
  environmentType: SystemEnvironment;
  host: string;
  port: number;
  serviceName?: string;
  pdbName?: string;
  username: string;
  password: string;
  standbyHost?: string;
  standbyPort?: number;
  standbyService?: string;
  description?: string;
}
