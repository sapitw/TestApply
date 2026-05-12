import React, { useEffect, useState } from 'react';
import { Button, Card, Form, Input, message, Modal, Space, Table, Tag } from 'antd';
import { dbaService, type TableAccessDto } from '../../services/dbaService';

const TableAccess: React.FC = () => {
  const [rows, setRows] = useState<TableAccessDto[]>([]);
  const [loading, setLoading] = useState(false);
  const [modal, setModal] = useState(false);
  const [form] = Form.useForm();

  const load = async () => {
    setLoading(true);
    const r = await dbaService.listTableAccess();
    if (r.success && r.data) setRows(r.data);
    setLoading(false);
  };
  useEffect(() => { load(); }, []);

  const submit = async () => {
    const v = await form.validateFields();
    const r = await dbaService.createTableAccess({
      ...v,
      hasPrimaryKey: v.hasPrimaryKey === 'true',
      hasUniqueIdx:  v.hasUniqueIdx === 'true',
      riskLevel: Number(v.riskLevel),
    });
    if (r.success) {
      message.success('已申请准入');
      setModal(false); form.resetFields(); load();
    }
  };

  const approve = async (id: string) => {
    const r = await dbaService.approveTableAccess(id);
    if (r.success) { message.success('已审批通过'); load(); }
  };

  return (
    <Card title="表准入管理（DBA 专属）" extra={<Button type="primary" onClick={() => setModal(true)}>申请新表准入</Button>}>
      <Table
        rowKey="accessId"
        loading={loading}
        dataSource={rows}
        size="small"
        scroll={{ x: 1100 }}
        columns={[
          { title: '表名', dataIndex: 'tableName' },
          { title: 'Schema', dataIndex: 'tableSchema' },
          { title: '连接 ID', dataIndex: 'connectionId' },
          { title: '主键', dataIndex: 'hasPrimaryKey', render: (v: boolean) => v ? <Tag color="green">是</Tag> : <Tag color="red">否</Tag> },
          { title: '唯一索引', dataIndex: 'hasUniqueIdx', render: (v: boolean) => v ? <Tag color="green">是</Tag> : <Tag color="red">否</Tag> },
          { title: '风险', dataIndex: 'riskLevel', render: (v: number) => <Tag color={v >= 3 ? 'red' : v === 2 ? 'orange' : 'green'}>{['低', '中', '高', '极高'][v - 1] || '低'}</Tag> },
          { title: '状态', dataIndex: 'status', render: (v: number) => v === 1 ? <Tag color="green">已准入</Tag> : <Tag>待审批</Tag> },
          { title: '创建时间', dataIndex: 'createdAt' },
          {
            title: '操作', key: 'action',
            render: (_: any, r: TableAccessDto) => r.status === 0
              ? <Button size="small" type="primary" onClick={() => approve(r.accessId)}>审批通过</Button>
              : '-',
          },
        ]}
      />
      <Modal title="申请表准入" open={modal} onCancel={() => setModal(false)} onOk={submit} destroyOnClose>
        <Form form={form} layout="vertical">
          <Form.Item name="connectionId" label="数据库连接 ID" rules={[{ required: true }]}><Input /></Form.Item>
          <Form.Item name="tableName" label="表名" rules={[{ required: true }]}><Input /></Form.Item>
          <Form.Item name="tableSchema" label="Schema"><Input /></Form.Item>
          <Form.Item name="hasPrimaryKey" label="是否有主键" initialValue="true">
            <Input placeholder="true / false" />
          </Form.Item>
          <Form.Item name="hasUniqueIdx" label="是否有唯一索引" initialValue="false">
            <Input placeholder="true / false" />
          </Form.Item>
          <Form.Item name="riskLevel" label="风险等级（1-4）" initialValue="1"><Input /></Form.Item>
        </Form>
      </Modal>
    </Card>
  );
};

export default TableAccess;
