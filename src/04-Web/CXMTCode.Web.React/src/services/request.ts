import axios, { type InternalAxiosRequestConfig } from 'axios';
import { message } from 'antd';
import { useAuthStore } from '../stores/authStore';

const request = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL || '/api',
  timeout: 30000,
  headers: { 'Content-Type': 'application/json' },
});

request.interceptors.request.use((config: InternalAxiosRequestConfig) => {
  const token = useAuthStore.getState().token;
  if (token) config.headers.Authorization = `Bearer ${token}`;
  return config;
});

request.interceptors.response.use(
  (response) => response.data,
  (error) => {
    const status = error.response?.status;
    const body = error.response?.data;
    if (status === 401) {
      useAuthStore.getState().logout();
      window.location.href = '/login';
    } else if (status === 403) {
      message.error(`权限不足：${body?.errorMessage || '无权访问'}`);
    } else if (status >= 500) {
      message.error(`服务器异常：${body?.errorMessage || error.message}`);
    } else if (body?.errorMessage) {
      message.error(body.errorMessage);
    }
    return Promise.reject(error);
  }
);

export default request;
