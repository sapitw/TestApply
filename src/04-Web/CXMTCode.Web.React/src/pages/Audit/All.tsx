import React, { useEffect, useState } from 'react';
import { Card, Table, Tag, DatePicker, Space, Button, Input } from 'antd';
import dayjs from 'dayjs';
import { auditService, type AuditLogEntry } from '../../services/auditService';
import { useAuthStore } from '../../stores/authStore';

const { RangePicker } = DatePicker;

const AuditAll: React.FC = () => {
  const [rows, setRows] = useState<AuditLogEntry[]>([]);
  const [loading, setLoading] = useState(false);
  const [range, setRange] = useState<[dayjs.Dayjs, dayjs.Dayjs] | null>(null);
  const [userIdFilter, setUserIdFilter] = useState('');
  const isSysAdmin = useAuthStore((s) => s.isSysAdmin());

  const load = async () => {
    setLoading(true);
    const r = await auditService.all({
      userId: userIdFilter || undefined,
      from: range?.[0]?.toISOString(),
      to: range?.[1]?.toISOString(),
    });
    if (r.success && r.data) setRows(r.data);
    setLoading(false);
  };
  useEffect(() => { load(); }, []);

  return (
    <Card title="全部审计记录（DBA 及以上）" extra={
      <Space>
        <Input placeholder="按用户 ID 过滤" allowClear value={userIdFilter} onChange={(e) => setUserIdFilter(e.target.value)} style={{ width: 220 }} />
        <RangePicker showTime onChange={(v) => setRange(v as any)} />
        <Button type="primary" onClick={load}>查询</Button>
      </Space>
    }>
      <Table
        rowKey="logId"
        loading={loading}
        dataSource={rows}
        size="small"
        scroll={{ x: 1200 }}
        columns={[
          { title: 'LogId', dataIndex: 'logId', width: 160 },
          { title: '时间', dataIndex: 'logTime', width: 180 },
          { title: '账号', dataIndex: 'userName', width: 120 },
          { title: '角色', dataIndex: 'userRole', width: 80,
            render: (r: number) => r === 3 ? 'SysAdmin' : r === 2 ? 'DBA' : 'User' },
          { title: '操作类型', dataIndex: 'operationType', width: 160 },
          { title: '描述', dataIndex: 'operationDesc' },
          { title: '环境', dataIndex: 'environmentType', width: 80,
            render: (v?: string) => v ? <Tag color={v === 'PROD' ? 'red' : 'green'}>{v}</Tag> : null },
          { title: '结果', dataIndex: 'result', width: 100,
            render: (v: string) => <Tag color={v === 'SUCCESS' ? 'green' : v === 'BLOCKED' ? 'red' : 'orange'}>{v}</Tag> },
          ...(isSysAdmin ? [{
            title: '校验', key: 'verify', width: 100,
            render: (_: any, r: AuditLogEntry) => (
              <Button size="small" onClick={async () => {
                const v = await auditService.verify(r.logId);
                v.data
                  ? (window as any).antd?.message?.success?.('哈希链一致')
                  : (window as any).antd?.message?.error?.('完整性破损');
              }}>验证</Button>
            ),
          }] : []),
        ]}
      />
    </Card>
  );
};

export default AuditAll;
