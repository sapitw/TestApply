export enum DatabaseType {
  Oracle19c = 1,
  MsSql2019 = 2,
  MySql80 = 3,
  Db2_115 = 4,
}

export const DatabaseTypeLabel: Record<number, string> = {
  1: 'Oracle 19C',
  2: 'MSSQL 2019',
  3: 'MySQL 8.0+',
  4: 'DB2 11.5~12.1',
};

export const DatabaseTypeColor: Record<number, string> = {
  1: 'red',
  2: 'blue',
  3: 'orange',
  4: 'purple',
};

export type SystemEnvironment = 'PROD' | 'TEST';

export enum SqlOperationType {
  Select = 1,
  Insert = 2,
  Update = 3,
  Delete = 4,
  Ddl = 5,
  Unknown = 99,
}

export const SqlOperationLabel: Record<number, string> = {
  1: 'SELECT',
  2: 'INSERT',
  3: 'UPDATE',
  4: 'DELETE',
  5: 'DDL',
  99: '其它',
};

export enum ChangeRequestStatus {
  Draft = 0,
  Submitted = 1,
  Approving = 2,
  Approved = 3,
  Rejected = 4,
  Executing = 5,
  Executed = 6,
  Failed = 7,
  RolledBack = 8,
  Cancelled = 9,
}

export const ChangeRequestStatusLabel: Record<number, string> = {
  0: '草稿',
  1: '已提交',
  2: '审批中',
  3: '已审批',
  4: '已驳回',
  5: '执行中',
  6: '已执行',
  7: '执行失败',
  8: '已回滚',
  9: '已取消',
};

export const ChangeRequestStatusColor: Record<number, string> = {
  0: 'default',
  1: 'blue',
  2: 'gold',
  3: 'cyan',
  4: 'red',
  5: 'processing',
  6: 'green',
  7: 'volcano',
  8: 'purple',
  9: 'default',
};

export interface ApiResponse<T> {
  success: boolean;
  data: T | null;
  errorMessage: string | null;
  errorCode: string | null;
  timestamp: string;
  traceId?: string | null;
}

export interface PageResult<T> {
  items: T[];
  totalCount: number;
  pageIndex: number;
  pageSize: number;
}

export interface MenuConfig {
  key: string;
  label: string;
  path: string;
  icon?: string;
  children?: MenuConfig[];
  requiredRole?: string | null;
}
