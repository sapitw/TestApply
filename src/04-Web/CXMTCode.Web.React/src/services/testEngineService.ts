import request from './request';
import type { ApiResponse } from '../types/common';

export interface TestSuiteDto {
  suiteId: string;
  suiteName: string;
  description?: string;
  isGate: boolean;
  minPassRate: number;
  createdBy?: string;
  createdAt: string;
}

export interface TestCaseDto {
  caseId: string;
  suiteId: string;
  caseName: string;
  caseType: 'UNIT' | 'INTEGRATION' | 'PERFORMANCE';
  targetSql?: string;
  expected?: string;
  ordinal: number;
  enabled: boolean;
}

export interface TestRunDto {
  runId: string;
  suiteId: string;
  status: 'QUEUED' | 'RUNNING' | 'PASSED' | 'FAILED';
  startedAt: string;
  finishedAt?: string;
  totalCases: number;
  passedCases: number;
  failedCases: number;
  passRate: number;
  triggeredBy?: string;
}

export const testEngineService = {
  listSuites: (): Promise<ApiResponse<TestSuiteDto[]>> => request.get('/admin/test/suites'),
  createSuite: (data: Partial<TestSuiteDto>): Promise<ApiResponse<string>> =>
    request.post('/admin/test/suites', data),
  listCases: (suiteId: string): Promise<ApiResponse<TestCaseDto[]>> =>
    request.get(`/admin/test/suites/${suiteId}/cases`),
  addCase: (suiteId: string, data: Partial<TestCaseDto>): Promise<ApiResponse<string>> =>
    request.post(`/admin/test/suites/${suiteId}/cases`, data),
  run: (suiteId: string): Promise<ApiResponse<TestRunDto>> =>
    request.post(`/admin/test/suites/${suiteId}/run`),
  listRuns: (suiteId?: string): Promise<ApiResponse<TestRunDto[]>> =>
    request.get('/admin/test/runs', { params: suiteId ? { suiteId } : undefined }),
  checkGate: (suiteId: string): Promise<ApiResponse<boolean>> =>
    request.get(`/admin/test/suites/${suiteId}/gate`),
};
