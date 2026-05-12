import React, { useState } from 'react';
import { Form, Input, Button, Card, Alert, Typography } from 'antd';
import { LockOutlined, UserOutlined } from '@ant-design/icons';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { authService } from '../../services/authService';
import { useAuthStore } from '../../stores/authStore';

const { Title } = Typography;

const LoginPage: React.FC = () => {
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const navigate = useNavigate();
  const [params] = useSearchParams();
  const login = useAuthStore((s) => s.login);

  const onFinish = async (values: { userName: string; password: string }) => {
    setLoading(true);
    setError(null);
    try {
      const r = await authService.login(values);
      if (r.success && r.data) {
        login(r.data);
        navigate(params.get('redirect') || '/dashboard', { replace: true });
      } else {
        setError(r.errorMessage || '登录失败');
      }
    } catch (e: any) {
      setError(e?.response?.data?.errorMessage || e.message);
    } finally {
      setLoading(false);
    }
  };

  return (
    <div style={{
      minHeight: '100vh',
      display: 'flex', alignItems: 'center', justifyContent: 'center',
      background: 'radial-gradient(circle at 30% 30%, #0d1b3a, #070d19 70%)'
    }}>
      <Card style={{ width: 420, padding: 24 }}>
        <div style={{ textAlign: 'center', marginBottom: 24 }}>
          <Title level={3} style={{
            background: 'linear-gradient(135deg,#0088ff,#00d4ff)',
            WebkitBackgroundClip: 'text', WebkitTextFillColor: 'transparent',
            margin: 0
          }}>CXMTCode 登录</Title>
          <div style={{ color: 'rgba(255,255,255,0.5)', marginTop: 4, fontSize: 13 }}>
            半导体行业多数据库变更管控平台 · V3.0
          </div>
        </div>
        {error && <Alert type="error" message={error} showIcon style={{ marginBottom: 16 }} closable onClose={() => setError(null)} />}
        <Form onFinish={onFinish} layout="vertical" autoComplete="off">
          <Form.Item name="userName" label="账号" rules={[{ required: true, message: '请输入账号' }]}>
            <Input prefix={<UserOutlined />} placeholder="用户名" autoFocus />
          </Form.Item>
          <Form.Item name="password" label="密码" rules={[{ required: true, message: '请输入密码' }]}>
            <Input.Password prefix={<LockOutlined />} placeholder="密码" />
          </Form.Item>
          <Form.Item>
            <Button type="primary" htmlType="submit" loading={loading} block size="large">
              登录
            </Button>
          </Form.Item>
        </Form>
        <div style={{ fontSize: 12, color: 'rgba(255,255,255,0.4)', marginTop: 12 }}>
          首次部署默认账号：<code>admin / admin@123</code>，登录后请到「系统管理 → 用户管理」修改密码。
        </div>
      </Card>
    </div>
  );
};

export default LoginPage;
