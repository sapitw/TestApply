import request from './request';
import type { ApiResponse } from '../types/common';
import type {
  ChangeRequestDto,
  DryRunResult,
  ExecutionResult,
  SubmitChangeRequest,
  SubmissionResult,
} from '../types/change';

export const changeService = {
  list: (): Promise<ApiResponse<ChangeRequestDto[]>> =>
    request.get('/change-requests'),
  get: (id: string): Promise<ApiResponse<ChangeRequestDto>> =>
    request.get(`/change-requests/${id}`),
  submit: (data: SubmitChangeRequest): Promise<ApiResponse<SubmissionResult>> =>
    request.post('/change-requests', data),
  dryRun: (id: string): Promise<ApiResponse<DryRunResult>> =>
    request.post(`/change-requests/${id}/dry-run`),
  execute: (id: string): Promise<ApiResponse<ExecutionResult>> =>
    request.post(`/change-requests/${id}/execute`),
  rollback: (id: string): Promise<ApiResponse<ExecutionResult>> =>
    request.post(`/change-requests/${id}/rollback`),
  approve: (id: string): Promise<ApiResponse<boolean>> =>
    request.post(`/change-requests/${id}/approve`),
  reject: (id: string): Promise<ApiResponse<boolean>> =>
    request.post(`/change-requests/${id}/reject`),
};
