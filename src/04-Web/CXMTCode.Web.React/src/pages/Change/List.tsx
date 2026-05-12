import React, { useEffect, useState } from 'react';
import { Card, Table, Tag, Button, Modal, message, Space } from 'antd';
import { useNavigate } from 'react-router-dom';
import { changeService } from '../../services/changeService';
import type { ChangeRequestDto } from '../../types/change';
import {
  ChangeRequestStatusColor,
  ChangeRequestStatusLabel,
  DatabaseTypeLabel,
  SqlOperationLabel,
} from '../../types/common';
import { useAuthStore } from '../../stores/authStore';

const ChangeList: React.FC = () => {
  const [rows, setRows] = useState<ChangeRequestDto[]>([]);
  const [loading, setLoading] = useState(false);
  const navigate = useNavigate();
  const isDbaOrAbove = useAuthStore((s) => s.isDbaOrAbove());

  const load = async () => {
    setLoading(true);
    const r = await changeService.list();
    if (r.success && r.data) setRows(r.data);
    setLoading(false);
  };

  useEffect(() => { load(); }, []);

  const doApprove = (id: string) => Modal.confirm({
    title: '审批通过此变更？',
    onOk: async () => {
      const r = await changeService.approve(id);
      if (r.success) { message.success('已审批通过'); load(); }
    },
  });

  const doReject = (id: string) => Modal.confirm({
    title: '驳回此变更？',
    onOk: async () => {
      const r = await changeService.reject(id);
      if (r.success) { message.success('已驳回'); load(); }
    },
  });

  return (
    <Card title="变更申请列表" extra={<Button type="primary" onClick={() => navigate('/change/apply')}>新建申请</Button>}>
      <Table
        rowKey="requestId"
        loading={loading}
        dataSource={rows}
        size="small"
        scroll={{ x: 1300 }}
        columns={[
          { title: '申请单号', dataIndex: 'requestId', key: 'requestId', fixed: 'left', width: 200 },
          { title: '状态', dataIndex: 'status', key: 'status', width: 100,
            render: (s: number) => <Tag color={ChangeRequestStatusColor[s]}>{ChangeRequestStatusLabel[s]}</Tag> },
          { title: '操作', dataIndex: 'operationType', key: 'op',
            render: (s: number) => SqlOperationLabel[s] },
          { title: 'DB 类型', dataIndex: 'databaseType', key: 'db',
            render: (s: number) => DatabaseTypeLabel[s] },
          { title: '目标表', dataIndex: 'targetTable', key: 'tbl' },
          { title: '影响行数', dataIndex: 'affectedRows', key: 'ar', render: (v?: number) => v ?? '-' },
          { title: '创建时间', dataIndex: 'createdAt', key: 'ct', width: 160 },
          {
            title: '操作', key: 'action', fixed: 'right', width: 320,
            render: (_: any, r: ChangeRequestDto) => (
              <Space size={4}>
                <Button size="small" onClick={() => navigate(`/change/detail/${r.requestId}`)}>详情</Button>
                <Button size="small" onClick={async () => {
                  const rs = await changeService.dryRun(r.requestId);
                  if (rs.success && rs.data) {
                    Modal.info({
                      title: 'DryRun 预演结果',
                      width: 720,
                      content: <pre style={{ fontSize: 12 }}>{JSON.stringify(rs.data, null, 2)}</pre>,
                    });
                    load();
                  }
                }}>预演</Button>
                {isDbaOrAbove && r.status === 1 && (
                  <>
                    <Button size="small" type="primary" onClick={() => doApprove(r.requestId)}>审批通过</Button>
                    <Button size="small" danger onClick={() => doReject(r.requestId)}>驳回</Button>
                  </>
                )}
                {r.status === 3 && (
                  <Button size="small" type="primary" onClick={async () => {
                    const rs = await changeService.execute(r.requestId);
                    if (rs.success) { message.success('执行已完成'); load(); }
                  }}>执行</Button>
                )}
                {r.status === 6 && (
                  <Button size="small" danger onClick={async () => {
                    const rs = await changeService.rollback(r.requestId);
                    if (rs.success) { message.success('回滚完成'); load(); }
                  }}>回滚</Button>
                )}
              </Space>
            ),
          },
        ]}
      />
    </Card>
  );
};

export default ChangeList;
