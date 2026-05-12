import React, { useEffect, useState } from 'react';
import { Card, Table, Tag, DatePicker, Space, Button } from 'antd';
import dayjs from 'dayjs';
import { auditService, type AuditLogEntry } from '../../services/auditService';

const { RangePicker } = DatePicker;

const AuditOwn: React.FC = () => {
  const [rows, setRows] = useState<AuditLogEntry[]>([]);
  const [loading, setLoading] = useState(false);
  const [range, setRange] = useState<[dayjs.Dayjs, dayjs.Dayjs] | null>(null);

  const load = async () => {
    setLoading(true);
    const r = await auditService.own({
      from: range?.[0]?.toISOString(),
      to: range?.[1]?.toISOString(),
    });
    if (r.success && r.data) setRows(r.data);
    setLoading(false);
  };
  useEffect(() => { load(); }, []);

  return (
    <Card title="我的审计记录" extra={
      <Space>
        <RangePicker showTime onChange={(v) => setRange(v as any)} />
        <Button type="primary" onClick={load}>查询</Button>
      </Space>
    }>
      <Table
        rowKey="logId"
        loading={loading}
        dataSource={rows}
        size="small"
        scroll={{ x: 1100 }}
        columns={[
          { title: 'LogId', dataIndex: 'logId', width: 160 },
          { title: '时间', dataIndex: 'logTime', width: 180 },
          { title: '操作类型', dataIndex: 'operationType', width: 160 },
          { title: '描述', dataIndex: 'operationDesc' },
          { title: '环境', dataIndex: 'environmentType', width: 80,
            render: (v?: string) => v ? <Tag color={v === 'PROD' ? 'red' : 'green'}>{v}</Tag> : null },
          { title: '结果', dataIndex: 'result', width: 100,
            render: (v: string) => <Tag color={v === 'SUCCESS' ? 'green' : v === 'BLOCKED' ? 'red' : 'orange'}>{v}</Tag> },
          { title: 'IP', dataIndex: 'clientIp', width: 140 },
        ]}
      />
    </Card>
  );
};

export default AuditOwn;
