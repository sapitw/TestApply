import React, { useEffect, useState } from 'react';
import { Button, Card, Form, Input, InputNumber, Modal, Popconfirm, Select, Table, message } from 'antd';
import { dbaService, type WhitelistRuleDto } from '../../services/dbaService';
import { DatabaseTypeLabel } from '../../types/common';

const Rules: React.FC = () => {
  const [rows, setRows] = useState<WhitelistRuleDto[]>([]);
  const [loading, setLoading] = useState(false);
  const [modal, setModal] = useState(false);
  const [form] = Form.useForm();

  const load = async () => {
    setLoading(true);
    const r = await dbaService.listRules();
    if (r.success && r.data) setRows(r.data);
    setLoading(false);
  };
  useEffect(() => { load(); }, []);

  const submit = async () => {
    const v = await form.validateFields();
    const r = await dbaService.createRule(v);
    if (r.success) { message.success('已新增规则'); setModal(false); form.resetFields(); load(); }
  };
  const remove = async (id: string) => {
    await dbaService.removeRule(id);
    message.success('已删除');
    load();
  };

  return (
    <Card title="白名单规则（DBA 专属）" extra={<Button type="primary" onClick={() => setModal(true)}>新增规则</Button>}>
      <Table
        rowKey="ruleId"
        loading={loading}
        dataSource={rows}
        size="small"
        columns={[
          { title: '规则名', dataIndex: 'ruleName' },
          { title: '表', dataIndex: 'tableName' },
          { title: 'DB', dataIndex: 'databaseType', render: (v: number) => DatabaseTypeLabel[v] },
          { title: '最大影响', dataIndex: 'maxAffected', width: 100 },
          { title: '时间窗口', dataIndex: 'timeWindow' },
          { title: '创建时间', dataIndex: 'createdAt' },
          {
            title: '操作', key: 'action',
            render: (_: any, r: WhitelistRuleDto) => (
              <Popconfirm title="确认删除规则？" onConfirm={() => remove(r.ruleId)}>
                <Button danger size="small">删除</Button>
              </Popconfirm>
            ),
          },
        ]}
      />
      <Modal title="新增白名单规则" open={modal} onCancel={() => setModal(false)} onOk={submit} destroyOnClose>
        <Form form={form} layout="vertical">
          <Form.Item name="ruleName" label="规则名" rules={[{ required: true }]}><Input /></Form.Item>
          <Form.Item name="tableName" label="表名" rules={[{ required: true }]}><Input /></Form.Item>
          <Form.Item name="databaseType" label="DB 类型" rules={[{ required: true }]}>
            <Select options={Object.entries(DatabaseTypeLabel).map(([v, l]) => ({ value: Number(v), label: l }))} />
          </Form.Item>
          <Form.Item name="maxAffected" label="最大影响行数" initialValue={1000}>
            <InputNumber min={1} style={{ width: '100%' }} />
          </Form.Item>
          <Form.Item name="timeWindow" label="允许时间窗口（HH:mm-HH:mm，可空）">
            <Input placeholder="如：00:00-06:00" />
          </Form.Item>
        </Form>
      </Modal>
    </Card>
  );
};

export default Rules;
