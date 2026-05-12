import type { ChangeRequestStatus, DatabaseType, SqlOperationType } from './common';

export interface ChangeRequestDto {
  requestId: string;
  status: ChangeRequestStatus;
  operationType: SqlOperationType;
  databaseType: DatabaseType;
  connectionId: string;
  targetTable: string;
  sqlStatement: string;
  reason: string;
  impactLevel: number;
  affectedRows?: number;
  applicantId: string;
  approverId?: string;
  approvedAt?: string;
  dryRunResult?: string;
  backupTable?: string;
  rollbackSql?: string;
  executedAt?: string;
  executedBy?: string;
  execResult?: string;
  rolledBackAt?: string;
  rolledBackBy?: string;
  createdAt: string;
  completedAt?: string;
}

export interface SubmitChangeRequest {
  operationType: SqlOperationType;
  databaseType: DatabaseType;
  connectionId: string;
  targetTable: string;
  sqlStatement: string;
  reason: string;
  impactLevel: number;
}

export interface SubmissionResult {
  success: boolean;
  requestId?: string;
  rejectReason?: string;
  ruleCode?: string;
}

export interface DryRunResult {
  success: boolean;
  affectedRows: number;
  plan?: { planText: string; estimatedRows: number; estimatedCost: number };
  errorMessage?: string;
  warnings: string[];
}

export interface ExecutionResult {
  success: boolean;
  affectedRows: number;
  executionTime: string;
  errorMessage?: string;
}
