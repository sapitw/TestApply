import React, { useEffect, useState } from 'react';
import { Card, Table, Tag, Button, Modal, Form, Input, Select, Tabs, Badge, message, Alert, InputNumber, Space, Popconfirm } from 'antd';
import { PlusOutlined, ThunderboltOutlined, EditOutlined, DeleteOutlined, EnvironmentOutlined } from '@ant-design/icons';
import { dbConnectionService } from '../../services/dbConnectionService';
import { DatabaseType, DatabaseTypeColor, DatabaseTypeLabel } from '../../types/common';
import type { DbConnectionDto, DbConnectionUpsertDto } from '../../types/dbConnection';
import { useEnvironmentStore } from '../../stores/environmentStore';

const DbConnections: React.FC = () => {
  const [rows, setRows] = useState<DbConnectionDto[]>([]);
  const [loading, setLoading] = useState(false);
  const [modal, setModal] = useState<{ visible: boolean; editing?: DbConnectionDto }>({ visible: false });
  const [form] = Form.useForm<DbConnectionUpsertDto>();
  const currentEnv = useEnvironmentStore((s) => s.current);

  const load = async () => {
    setLoading(true);
    const r = await dbConnectionService.list();
    if (r.success && r.data) setRows(r.data);
    setLoading(false);
  };
  useEffect(() => { load(); }, []);

  const open = (r?: DbConnectionDto) => {
    form.resetFields();
    if (r) form.setFieldsValue({ ...r, password: '' });
    setModal({ visible: true, editing: r });
  };

  const submit = async () => {
    const v = await form.validateFields();
    const op = modal.editing
      ? dbConnectionService.update(modal.editing.connectionId, v)
      : dbConnectionService.create(v);
    const r = await op;
    if (r.success) {
      message.success(modal.editing ? '更新成功' : '新增成功');
      setModal({ visible: false });
      load();
    }
  };

  const doTest = async (id: string) => {
    const r = await dbConnectionService.test(id);
    if (r.success && r.data) {
      r.data.success
        ? message.success(`✅ 连接成功：${r.data.databaseVersion}`)
        : message.error(`❌ ${r.data.errorMessage}`);
    }
  };

  const doDelete = async (id: string) => {
    await dbConnectionService.remove(id);
    message.success('已删除');
    load();
  };

  const doActivate = async (id: string) => {
    await dbConnectionService.activate(id);
    message.success('已设为当前环境激活连接');
    load();
  };

  const columns = [
    { title: '连接名称', dataIndex: 'connectionName', key: 'name', width: 200 },
    { title: '环境', dataIndex: 'environmentType', key: 'env', width: 100,
      render: (v: string) => <Tag color={v === 'PROD' ? 'red' : 'green'}>{v === 'PROD' ? '🔴 生产' : '🟢 测试'}</Tag> },
    { title: '数据库', dataIndex: 'databaseType', key: 'db', width: 140,
      render: (t: DatabaseType) => <Tag color={DatabaseTypeColor[t]}>{DatabaseTypeLabel[t]}</Tag> },
    { title: '主机', dataIndex: 'host', key: 'host' },
    { title: '端口', dataIndex: 'port', key: 'port', width: 80 },
    { title: '服务名', dataIndex: 'serviceName', key: 'svc' },
    { title: '激活', dataIndex: 'isActiveEnv', key: 'active', width: 100,
      render: (v: boolean) => v
        ? <Badge status="processing" text={<span style={{ color: '#1890ff', fontWeight: 600 }}>当前激活</span>} />
        : <Badge status="default" text="-" /> },
    {
      title: '操作', key: 'action', width: 280,
      render: (_: any, r: DbConnectionDto) => (
        <Space size={4}>
          <Button size="small" icon={<ThunderboltOutlined />} onClick={() => doTest(r.connectionId)}>测试</Button>
          <Button size="small" icon={<EditOutlined />} onClick={() => open(r)}>编辑</Button>
          <Button size="small" onClick={() => doActivate(r.connectionId)}>激活</Button>
          <Popconfirm title="确认删除？" onConfirm={() => doDelete(r.connectionId)}>
            <Button size="small" danger icon={<DeleteOutlined />}>删除</Button>
          </Popconfirm>
        </Space>
      ),
    },
  ];

  const prod = rows.filter((r) => r.environmentType === 'PROD');
  const test = rows.filter((r) => r.environmentType === 'TEST');

  return (
    <Card title="数据库连接配置（DBA 专属）" extra={<Button type="primary" icon={<PlusOutlined />} onClick={() => open()}>新增连接</Button>}>
      <Alert
        type={currentEnv === 'PROD' ? 'error' : 'success'}
        showIcon
        icon={<EnvironmentOutlined />}
        style={{ marginBottom: 16 }}
        message={<span>当前系统环境：<Tag color={currentEnv === 'PROD' ? 'red' : 'green'}>{currentEnv === 'PROD' ? '🔴 生产环境' : '🟢 测试环境'}</Tag></span>}
      />
      <Tabs
        items={[
          { key: 'all', label: `全部 (${rows.length})`, children: <Table size="small" rowKey="connectionId" loading={loading} columns={columns} dataSource={rows} scroll={{ x: 1200 }} /> },
          { key: 'prod', label: <span><Tag color="red">PROD</Tag>生产 ({prod.length})</span>, children: <Table size="small" rowKey="connectionId" columns={columns} dataSource={prod} scroll={{ x: 1200 }} /> },
          { key: 'test', label: <span><Tag color="green">TEST</Tag>测试 ({test.length})</span>, children: <Table size="small" rowKey="connectionId" columns={columns} dataSource={test} scroll={{ x: 1200 }} /> },
        ]}
      />

      <Modal
        title={modal.editing ? '编辑数据库连接' : '新增数据库连接'}
        open={modal.visible}
        onCancel={() => setModal({ visible: false })}
        onOk={submit}
        width={760}
        destroyOnClose
      >
        <Form form={form} layout="vertical">
          <Form.Item name="connectionName" label="连接名称" rules={[{ required: true }]}>
            <Input placeholder="如：MES 生产库 Oracle 主" />
          </Form.Item>
          <Form.Item name="environmentType" label="环境类型" rules={[{ required: true }]}>
            <Select options={[
              { value: 'PROD', label: '🔴 生产环境 PROD' },
              { value: 'TEST', label: '🟢 测试环境 TEST' },
            ]} />
          </Form.Item>
          <Form.Item name="databaseType" label="数据库类型" rules={[{ required: true }]}>
            <Select options={Object.entries(DatabaseTypeLabel).map(([v, l]) => ({ value: Number(v), label: l }))} />
          </Form.Item>
          <Form.Item name="host" label="主机地址" rules={[{ required: true }]}><Input /></Form.Item>
          <Form.Item name="port" label="端口" rules={[{ required: true }]}><InputNumber min={1} max={65535} style={{ width: '100%' }} /></Form.Item>
          <Form.Item name="serviceName" label="服务名 / Schema"><Input /></Form.Item>
          <Form.Item name="pdbName" label="PDB 名称（Oracle CDB）"><Input /></Form.Item>
          <Form.Item name="username" label="账号" rules={[{ required: true }]}><Input /></Form.Item>
          <Form.Item name="password" label={modal.editing ? '密码（留空表示不修改）' : '密码'} rules={modal.editing ? [] : [{ required: true }]}>
            <Input.Password placeholder="将通过 SM4 加密存储" />
          </Form.Item>
          <Form.Item name="standbyHost" label="备库主机（预演用）"><Input /></Form.Item>
          <Form.Item name="standbyPort" label="备库端口"><InputNumber min={1} max={65535} style={{ width: '100%' }} /></Form.Item>
          <Form.Item name="standbyService" label="备库服务名"><Input /></Form.Item>
          <Form.Item name="description" label="备注"><Input.TextArea rows={2} /></Form.Item>
        </Form>
      </Modal>
    </Card>
  );
};

export default DbConnections;
