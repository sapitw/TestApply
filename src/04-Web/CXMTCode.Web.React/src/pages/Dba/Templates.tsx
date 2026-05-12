import React, { useEffect, useState } from 'react';
import { Button, Card, Form, Input, InputNumber, Modal, Table, Tag, message } from 'antd';
import { dbaService, type DeleteTemplateDto } from '../../services/dbaService';

const Templates: React.FC = () => {
  const [rows, setRows] = useState<DeleteTemplateDto[]>([]);
  const [loading, setLoading] = useState(false);
  const [modal, setModal] = useState(false);
  const [form] = Form.useForm();

  const load = async () => {
    setLoading(true);
    const r = await dbaService.listTemplates();
    if (r.success && r.data) setRows(r.data);
    setLoading(false);
  };
  useEffect(() => { load(); }, []);

  const submit = async () => {
    const v = await form.validateFields();
    const r = await dbaService.createTemplate(v);
    if (r.success) { message.success('已新增模板'); setModal(false); form.resetFields(); load(); }
  };
  const approve = async (id: string) => {
    const r = await dbaService.approveTemplate(id);
    if (r.success) { message.success('已激活'); load(); }
  };

  return (
    <Card title="DELETE 模板管理（DBA 专属）" extra={<Button type="primary" onClick={() => setModal(true)}>新增模板</Button>}>
      <Table
        rowKey="templateId"
        loading={loading}
        dataSource={rows}
        size="small"
        scroll={{ x: 1200 }}
        columns={[
          { title: '模板名称', dataIndex: 'templateName' },
          { title: '表', dataIndex: 'tableName' },
          { title: '最大影响行数', dataIndex: 'maxAffectedRows', width: 120 },
          { title: '版本', dataIndex: 'version', width: 80 },
          { title: '状态', dataIndex: 'status', render: (v: number) => v === 2 ? <Tag color="green">已激活</Tag> : v === 1 ? <Tag color="gold">待审批</Tag> : <Tag>草稿</Tag> },
          { title: 'SQL 模板', dataIndex: 'templateSql',
            render: (v: string) => <code style={{ fontSize: 11 }}>{v.length > 80 ? `${v.slice(0, 80)}…` : v}</code> },
          {
            title: '操作', key: 'action',
            render: (_: any, r: DeleteTemplateDto) => r.status !== 2
              ? <Button size="small" type="primary" onClick={() => approve(r.templateId)}>审批激活</Button>
              : '-',
          },
        ]}
      />
      <Modal title="新增 DELETE 模板" open={modal} onCancel={() => setModal(false)} onOk={submit} width={680} destroyOnClose>
        <Form form={form} layout="vertical">
          <Form.Item name="templateName" label="模板名称" rules={[{ required: true }]}><Input /></Form.Item>
          <Form.Item name="connectionId" label="数据库连接 ID" rules={[{ required: true }]}><Input /></Form.Item>
          <Form.Item name="tableName" label="表名" rules={[{ required: true }]}><Input /></Form.Item>
          <Form.Item name="templateSql" label="模板 SQL（参数用 :name 或 ?）" rules={[{ required: true }]}>
            <Input.TextArea rows={5} placeholder="如：DELETE FROM ORDERS WHERE STATUS = :status AND CREATED_AT &lt; :before" />
          </Form.Item>
          <Form.Item name="maxAffectedRows" label="最大影响行数" initialValue={1000}>
            <InputNumber min={1} style={{ width: '100%' }} />
          </Form.Item>
        </Form>
      </Modal>
    </Card>
  );
};

export default Templates;
