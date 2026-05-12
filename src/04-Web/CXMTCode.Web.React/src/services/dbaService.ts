import request from './request';
import type { ApiResponse } from '../types/common';
import type { DatabaseType } from '../types/common';

export interface TableAccessDto {
  accessId: string;
  connectionId: string;
  tableName: string;
  tableSchema?: string;
  status: number;
  dbaApprover?: string;
  bizApprover?: string;
  hasPrimaryKey: boolean;
  hasUniqueIdx: boolean;
  riskLevel: number;
  createdAt: string;
  approvedAt?: string;
}

export interface DeleteTemplateDto {
  templateId: string;
  templateName: string;
  connectionId: string;
  tableName: string;
  templateSql: string;
  parametersJson?: string;
  maxAffectedRows: number;
  status: number;
  version: number;
  createdAt?: string;
  approvedAt?: string;
}

export interface WhitelistRuleDto {
  ruleId: string;
  ruleName: string;
  tableName: string;
  databaseType: DatabaseType;
  allowedColumns?: string;
  maxAffected: number;
  timeWindow?: string;
  status: number;
  createdAt?: string;
}

export const dbaService = {
  listTableAccess: (): Promise<ApiResponse<TableAccessDto[]>> =>
    request.get('/dba/table-access'),
  createTableAccess: (data: Partial<TableAccessDto>): Promise<ApiResponse<TableAccessDto>> =>
    request.post('/dba/table-access', data),
  approveTableAccess: (id: string): Promise<ApiResponse<boolean>> =>
    request.post(`/dba/table-access/${id}/approve`),

  listTemplates: (): Promise<ApiResponse<DeleteTemplateDto[]>> =>
    request.get('/dba/templates'),
  createTemplate: (data: Partial<DeleteTemplateDto>): Promise<ApiResponse<DeleteTemplateDto>> =>
    request.post('/dba/templates', data),
  approveTemplate: (id: string): Promise<ApiResponse<boolean>> =>
    request.post(`/dba/templates/${id}/approve`),

  listRules: (): Promise<ApiResponse<WhitelistRuleDto[]>> => request.get('/dba/rules'),
  createRule: (data: Partial<WhitelistRuleDto>): Promise<ApiResponse<WhitelistRuleDto>> =>
    request.post('/dba/rules', data),
  removeRule: (id: string): Promise<ApiResponse<boolean>> => request.delete(`/dba/rules/${id}`),
};
