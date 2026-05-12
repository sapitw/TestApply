import React from 'react';
import { Card, Col, Row, Tag, Space } from 'antd';
import {
  DatabaseOutlined,
  SecurityScanOutlined,
  SafetyOutlined,
  UserOutlined,
  CheckCircleOutlined,
  GlobalOutlined,
} from '@ant-design/icons';
import { useAuthStore } from '../../stores/authStore';
import { UserRoleLabel } from '../../types/user';
import { useEnvironmentStore } from '../../stores/environmentStore';

const indicators = [
  { icon: <DatabaseOutlined />, value: '4', unit: '种', label: '适配数据库', desc: 'Oracle / MSSQL / MySQL / DB2' },
  { icon: <SecurityScanOutlined />, value: '25', unit: '+', label: '原子插件', desc: '微内核插件化，热插拔' },
  { icon: <UserOutlined />, value: '3', unit: '级', label: 'RBAC 权限', desc: 'User / DBA / SysAdmin' },
  { icon: <SafetyOutlined />, value: '10', unit: '条', label: '硬编码安全红线', desc: '不可修改 / 不可关闭 / 不可绕过' },
  { icon: <CheckCircleOutlined />, value: '100', unit: '%', label: '审计覆盖率', desc: 'Append Only · SM3 哈希链' },
  { icon: <GlobalOutlined />, value: '3', unit: '年+', label: '审计日志留存', desc: '按季度分区，可热数据归档' },
];

const Dashboard: React.FC = () => {
  const user = useAuthStore((s) => s.user);
  const env = useEnvironmentStore((s) => s.current);
  return (
    <div>
      <Card style={{ marginBottom: 16 }}>
        <Space size="large" style={{ width: '100%', justifyContent: 'space-between' }}>
          <div>
            <h2 style={{ margin: 0, color: '#fff' }}>欢迎，{user?.displayName || user?.userName}</h2>
            <div style={{ color: 'rgba(255,255,255,0.6)', marginTop: 4 }}>
              当前角色：<Tag color="blue">{UserRoleLabel[user?.role || 'User']}</Tag>
              当前环境：<Tag color={env === 'PROD' ? 'red' : 'green'}>{env}</Tag>
            </div>
          </div>
        </Space>
      </Card>

      <Row gutter={[16, 16]}>
        {indicators.map((it, i) => (
          <Col xs={24} sm={12} md={8} key={i}>
            <Card className="data-card" bodyStyle={{ padding: 24 }}>
              <div style={{ fontSize: 28, color: '#0088ff' }}>{it.icon}</div>
              <div className="number">{it.value}<span style={{ fontSize: 22 }}>{it.unit}</span></div>
              <div style={{ fontSize: 15, color: '#fff', marginTop: 8, fontWeight: 600 }}>{it.label}</div>
              <div style={{ fontSize: 12, color: 'rgba(255,255,255,0.5)', marginTop: 4 }}>{it.desc}</div>
            </Card>
          </Col>
        ))}
      </Row>
    </div>
  );
};

export default Dashboard;
