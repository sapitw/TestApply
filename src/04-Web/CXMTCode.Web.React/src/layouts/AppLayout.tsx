import React, { useEffect, useState } from 'react';
import { Layout, Menu, Dropdown, Avatar, Tag, Space, Spin } from 'antd';
import { UserOutlined, LogoutOutlined } from '@ant-design/icons';
import * as Icons from '@ant-design/icons';
import { Link, Outlet, useLocation, useNavigate } from 'react-router-dom';
import { useAuthStore } from '../stores/authStore';
import { UserRoleColor, UserRoleLabel } from '../types/user';
import type { MenuConfig } from '../types/common';
import { authService } from '../services/authService';
import EnvironmentBanner from '../components/EnvironmentBanner';

const { Sider, Header, Content } = Layout;

function pickIcon(name?: string): React.ReactNode {
  if (!name) return null;
  const I = (Icons as unknown as Record<string, React.ComponentType>)[name];
  return I ? <I /> : null;
}

function buildAntMenu(items: MenuConfig[]): any[] {
  return items.map((m) => ({
    key: m.path || m.key,
    icon: pickIcon(m.icon),
    label: m.children?.length ? m.label : <Link to={m.path}>{m.label}</Link>,
    children: m.children?.length ? buildAntMenu(m.children) : undefined,
  }));
}

const AppLayout: React.FC = () => {
  const { user, logout } = useAuthStore();
  const navigate = useNavigate();
  const location = useLocation();
  const [menus, setMenus] = useState<MenuConfig[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    authService.menus().then((r) => {
      if (r.success && r.data) setMenus(r.data);
      setLoading(false);
    });
  }, []);

  const onLogout = () => {
    logout();
    navigate('/login');
  };

  return (
    <Layout style={{ minHeight: '100vh' }}>
      <EnvironmentBanner />
      <Layout className="cxmt-content">
        <Sider width={232} theme="dark">
          <div style={{ height: 56, display: 'flex', alignItems: 'center', justifyContent: 'center', padding: '0 16px' }}>
            <span style={{
              fontSize: 18, fontWeight: 700, letterSpacing: 2,
              background: 'linear-gradient(135deg,#0088ff,#00d4ff)',
              WebkitBackgroundClip: 'text', WebkitTextFillColor: 'transparent'
            }}>CXMTCode</span>
          </div>
          {loading ? (
            <div style={{ textAlign: 'center', marginTop: 32 }}><Spin /></div>
          ) : (
            <Menu
              theme="dark"
              mode="inline"
              selectedKeys={[location.pathname]}
              defaultOpenKeys={menus.map((m) => m.path || m.key)}
              items={buildAntMenu(menus)}
            />
          )}
        </Sider>
        <Layout>
          <Header style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', padding: '0 24px' }}>
            <h2 style={{ color: '#fff', margin: 0, fontSize: 16 }}>
              半导体行业多数据库变更管控平台 · V3.0
            </h2>
            <Space>
              {user && (
                <Tag color={UserRoleColor[user.role]}>
                  {UserRoleLabel[user.role]}
                </Tag>
              )}
              <Dropdown
                menu={{
                  items: [
                    { key: 'profile', label: <Link to="/profile">个人资料</Link>, icon: <UserOutlined /> },
                    { type: 'divider' },
                    { key: 'logout', label: '退出登录', icon: <LogoutOutlined />, onClick: onLogout },
                  ],
                }}
              >
                <Space style={{ cursor: 'pointer', color: '#fff' }}>
                  <Avatar size="small" icon={<UserOutlined />} />
                  <span>{user?.displayName || user?.userName}</span>
                </Space>
              </Dropdown>
            </Space>
          </Header>
          <Content className="cxmt-page">
            <Outlet />
          </Content>
        </Layout>
      </Layout>
    </Layout>
  );
};

export default AppLayout;
