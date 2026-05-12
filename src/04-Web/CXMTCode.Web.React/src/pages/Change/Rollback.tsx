import React, { useEffect, useState } from 'react';
import { Card, Table, Tag, Button, message, Modal } from 'antd';
import { changeService } from '../../services/changeService';
import type { ChangeRequestDto } from '../../types/change';
import { ChangeRequestStatus, ChangeRequestStatusLabel } from '../../types/common';

const Rollback: React.FC = () => {
  const [rows, setRows] = useState<ChangeRequestDto[]>([]);
  const [loading, setLoading] = useState(false);

  const load = async () => {
    setLoading(true);
    const r = await changeService.list();
    if (r.success && r.data)
      setRows(r.data.filter((x) => x.status === ChangeRequestStatus.Executed));
    setLoading(false);
  };

  useEffect(() => { load(); }, []);

  const doRollback = (id: string) => Modal.confirm({
    title: '确认回滚此变更？',
    content: '将在主库执行已生成的回滚 SQL。',
    onOk: async () => {
      const r = await changeService.rollback(id);
      if (r.success && r.data) {
        r.data.success ? message.success('回滚已完成') : message.error(r.data.errorMessage || '回滚失败');
        load();
      }
    },
  });

  return (
    <Card title="回滚操作 · 只显示已执行的变更">
      <Table
        rowKey="requestId"
        size="small"
        loading={loading}
        dataSource={rows}
        columns={[
          { title: '申请单号', dataIndex: 'requestId' },
          { title: '状态', dataIndex: 'status', render: (s: number) => <Tag>{ChangeRequestStatusLabel[s]}</Tag> },
          { title: '目标表', dataIndex: 'targetTable' },
          { title: '执行时间', dataIndex: 'executedAt' },
          {
            title: '操作', key: 'action',
            render: (_: any, r: ChangeRequestDto) => (
              <Button danger size="small" onClick={() => doRollback(r.requestId)}>一键回滚</Button>
            ),
          },
        ]}
      />
    </Card>
  );
};

export default Rollback;
