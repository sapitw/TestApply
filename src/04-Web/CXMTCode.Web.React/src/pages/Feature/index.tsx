import React from 'react';
import { Card, Col, Row, Tag, Timeline } from 'antd';
import {
  BranchesOutlined,
  CheckCircleOutlined,
  DatabaseOutlined,
  GlobalOutlined,
  SafetyOutlined,
  SecurityScanOutlined,
  ThunderboltOutlined,
  UserOutlined,
} from '@ant-design/icons';

const FeatureSummary: React.FC = () => (
  <div>
    <div style={{
      background: 'linear-gradient(135deg, #070d19, #0d1b3a, #070d19)',
      padding: '60px 40px',
      textAlign: 'center',
      borderBottom: '1px solid rgba(0,136,255,0.15)',
      marginBottom: 24,
      borderRadius: 12,
    }}>
      <h1 className="cxmt-hero-title">CXMTCode 功能全景</h1>
      <p style={{ fontSize: 16, color: 'rgba(255,255,255,0.6)', maxWidth: 800, margin: '16px auto 0' }}>
        半导体行业多数据库变更管控平台 · 微内核架构 · 国密安全 · 国产化适配
      </p>
      <div style={{ marginTop: 20, display: 'flex', justifyContent: 'center', gap: 10, flexWrap: 'wrap' }}>
        {['C# 12 + .NET 8.0 LTS', 'React 18 + TypeScript', 'Oracle 19C 系统DB',
          '国密 SM2/SM3/SM4', 'CXMT 深蓝科技风格'].map((t) => (
          <Tag key={t} color="blue" style={{ fontSize: 13, padding: '4px 12px' }}>{t}</Tag>
        ))}
      </div>
    </div>

    <Row gutter={[16, 16]} style={{ marginBottom: 24 }}>
      {[
        { icon: <DatabaseOutlined />, v: '4', s: '种', l: '适配数据库', d: 'Oracle 19C / MSSQL 2019 / MySQL 8.0+ / DB2 11.5~12.1' },
        { icon: <SecurityScanOutlined />, v: '25', s: '+', l: '原子插件', d: 'B/C/D/E/F 5 大类共 25+ 个' },
        { icon: <UserOutlined />, v: '3', s: '级', l: 'RBAC 权限', d: 'User / DBA / SysAdmin' },
        { icon: <SafetyOutlined />, v: '10', s: '条', l: '硬编码安全红线', d: '不可修改 / 关闭 / 绕过' },
        { icon: <CheckCircleOutlined />, v: '100', s: '%', l: '审计覆盖率', d: 'Append Only · SM3 哈希链' },
        { icon: <GlobalOutlined />, v: '3', s: '年+', l: '日志留存', d: '按季度分区，自动归档' },
      ].map((it, i) => (
        <Col xs={24} sm={12} md={8} key={i}>
          <Card className="data-card" bodyStyle={{ padding: 24 }}>
            <div style={{ fontSize: 28, color: '#0088ff', marginBottom: 8 }}>{it.icon}</div>
            <div className="number">{it.v}<span style={{ fontSize: 22 }}>{it.s}</span></div>
            <div style={{ fontSize: 15, color: '#fff', marginTop: 8, fontWeight: 600 }}>{it.l}</div>
            <div style={{ fontSize: 12, color: 'rgba(255,255,255,0.5)', marginTop: 4 }}>{it.d}</div>
          </Card>
        </Col>
      ))}
    </Row>

    <Card title={<span><ThunderboltOutlined /> 六层架构</span>} style={{ marginBottom: 24 }}>
      {[
        { layer: '前端应用层', color: '#0088ff', items: ['React 18 + TypeScript', 'Ant Design X', 'dnd-kit', 'CXMT 深蓝科技风格', 'RBAC 路由', 'TEST/PROD 横幅'] },
        { layer: '业务场景组合层', color: '#00d4aa', items: ['变更申请', '审计查询', 'DB 连接管理', '测试引擎'] },
        { layer: '插件化原子单元', color: '#ffaa00', items: ['C1-C4 DB 适配', 'B1-B5 安全管控', 'D1-D7 执行回滚', 'E1-E7 流程审计', 'F1-F5 测试引擎'] },
        { layer: '微内核基础底座', color: '#ff4466', items: ['权限校验', '审计日志', '任务调度', '分布式事务', '配置管理', '插件生命周期'] },
        { layer: '系统存储层', color: '#aa66ff', items: ['Oracle 19C 系统DB', 'CXMT_ 前缀', '审计季度分区', 'SM4 加密存储'] },
        { layer: '集成适配层', color: '#00d4ff', items: ['Oracle / MSSQL / MySQL / DB2 驱动', 'ANTLR4 SQL 解析', 'Workflow-Core', 'GMSSL'] },
      ].map((l, i) => (
        <div key={i} style={{
          display: 'flex', alignItems: 'center', gap: 16, padding: '12px 20px',
          background: 'rgba(255,255,255,0.02)', borderRadius: 8,
          borderLeft: `4px solid ${l.color}`, marginBottom: 8
        }}>
          <span style={{ color: l.color, fontWeight: 600, minWidth: 140 }}>{l.layer}</span>
          <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap' }}>
            {l.items.map((it, j) => (
              <Tag key={j} style={{ background: `${l.color}15`, color: l.color, border: `1px solid ${l.color}30` }}>{it}</Tag>
            ))}
          </div>
        </div>
      ))}
    </Card>

    <Card title={<span><BranchesOutlined /> V3.0 核心亮点</span>}>
      <Timeline mode="alternate" items={[
        { color: '#0088ff', children: <div><h4 style={{ color: '#0088ff', margin: 0 }}>Oracle 19C 系统数据库</h4><p style={{ color: 'rgba(255,255,255,0.6)', fontSize: 13 }}>13 张核心表，CXMT_ 前缀，审计按季度分区，SM4 加密</p></div> },
        { color: '#ffaa00', children: <div><h4 style={{ color: '#ffaa00', margin: 0 }}>DB 连接 TEST/PROD 管理</h4><p style={{ color: 'rgba(255,255,255,0.6)', fontSize: 13 }}>DBA 专属，每条连接标注环境类型，支持连接测试 + 激活</p></div> },
        { color: '#ff4466', children: <div><h4 style={{ color: '#ff4466', margin: 0 }}>SysAdmin 环境切换</h4><p style={{ color: 'rgba(255,255,255,0.6)', fontSize: 13 }}>顶部单一开关切换全局 TEST/PROD，自动激活对应 DB 连接，全程留痕</p></div> },
        { color: '#00d4aa', children: <div><h4 style={{ color: '#00d4aa', margin: 0 }}>环境标识横幅</h4><p style={{ color: 'rgba(255,255,255,0.6)', fontSize: 13 }}>顶部固定，PROD 红色警示 / TEST 绿色标识，所有用户清晰可见</p></div> },
        { color: '#aa66ff', children: <div><h4 style={{ color: '#aa66ff', margin: 0 }}>CXMT 深蓝科技风格</h4><p style={{ color: 'rgba(255,255,255,0.6)', fontSize: 13 }}>深蓝背景 + 科技蓝强调 + 毛玻璃卡片 + 数据驱动 KPI</p></div> },
      ]} />
    </Card>
  </div>
);

export default FeatureSummary;
