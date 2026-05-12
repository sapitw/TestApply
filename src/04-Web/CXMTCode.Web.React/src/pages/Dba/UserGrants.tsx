import React, { useEffect, useState } from 'react';
import { Button, Card, Checkbox, Form, Input, message, Modal, Popconfirm, Select, Space, Table, Tag } from 'antd';
import { userGrantService, type UserGrantDto } from '../../services/userGrantService';
import { dbaService, type TableAccessDto } from '../../services/dbaService';
import { adminService } from '../../services/adminService';
import type { UserInfo } from '../../types/user';

const UserGrants: React.FC = () => {
  const [accessList, setAccessList] = useState<TableAccessDto[]>([]);
  const [users, setUsers] = useState<UserInfo[]>([]);
  const [selectedAccess, setSelectedAccess] = useState<string | undefined>();
  const [grants, setGrants] = useState<UserGrantDto[]>([]);
  const [loading, setLoading] = useState(false);
  const [modal, setModal] = useState(false);
  const [form] = Form.useForm();

  useEffect(() => {
    dbaService.listTableAccess().then((r) => {
      if (r.success && r.data) setAccessList(r.data.filter((a) => a.status === 1));
    });
    // 用户列表 - 失败时（无 SysAdmin 权限）静默
    adminService.listUsers().then((r) => {
      if (r.success && r.data) setUsers(r.data);
    }).catch(() => { /* DBA 可能没有该权限 */ });
  }, []);

  const loadGrants = async (accessId?: string) => {
    if (!accessId) return;
    setLoading(true);
    const r = await userGrantService.listByAccess(accessId);
    if (r.success && r.data) setGrants(r.data);
    setLoading(false);
  };

  useEffect(() => { loadGrants(selectedAccess); }, [selectedAccess]);

  const submit = async () => {
    const v = await form.validateFields();
    if (!selectedAccess) { message.error('请先选择已准入的表'); return; }
    const r = await userGrantService.grant({
      accessId: selectedAccess,
      userId: v.userId,
      allowSelect: !!v.allow?.includes('SELECT'),
      allowInsert: !!v.allow?.includes('INSERT'),
      allowUpdate: !!v.allow?.includes('UPDATE'),
      allowDelete: !!v.allow?.includes('DELETE'),
    });
    if (r.success) {
      message.success('已授权');
      setModal(false); form.resetFields();
      loadGrants(selectedAccess);
    }
  };

  const revoke = async (id: string) => {
    await userGrantService.revoke(id);
    message.success('已撤销');
    loadGrants(selectedAccess);
  };

  return (
    <Card title="用户表级授权（DBA 专属）" extra={
      <Space>
        <Select
          style={{ width: 360 }}
          placeholder="选择已准入的表"
          value={selectedAccess}
          onChange={setSelectedAccess}
          options={accessList.map((a) => ({
            value: a.accessId,
            label: `${a.tableSchema ?? ''}${a.tableSchema ? '.' : ''}${a.tableName}`,
          }))}
        />
        <Button type="primary" disabled={!selectedAccess} onClick={() => setModal(true)}>新增授权</Button>
      </Space>
    }>
      {!selectedAccess && <div style={{ color: 'rgba(255,255,255,0.5)' }}>请先选择上方下拉中的一张已准入表</div>}
      {selectedAccess && (
        <Table
          rowKey="permissionId"
          loading={loading}
          dataSource={grants}
          size="small"
          columns={[
            { title: '用户 ID', dataIndex: 'userId' },
            { title: 'SELECT', dataIndex: 'allowSelect', render: (v: boolean) => v ? <Tag color="green">✓</Tag> : <Tag>✗</Tag> },
            { title: 'INSERT', dataIndex: 'allowInsert', render: (v: boolean) => v ? <Tag color="green">✓</Tag> : <Tag>✗</Tag> },
            { title: 'UPDATE', dataIndex: 'allowUpdate', render: (v: boolean) => v ? <Tag color="green">✓</Tag> : <Tag>✗</Tag> },
            { title: 'DELETE', dataIndex: 'allowDelete', render: (v: boolean) => v ? <Tag color="green">✓</Tag> : <Tag>✗</Tag> },
            { title: '授权时间', dataIndex: 'grantedAt' },
            {
              title: '操作', key: 'action',
              render: (_: any, r: UserGrantDto) => (
                <Popconfirm title="撤销该用户对此表的权限？" onConfirm={() => revoke(r.permissionId)}>
                  <Button danger size="small">撤销</Button>
                </Popconfirm>
              ),
            },
          ]}
        />
      )}

      <Modal title="新增表级授权" open={modal} onCancel={() => setModal(false)} onOk={submit} destroyOnClose>
        <Form form={form} layout="vertical">
          <Form.Item name="userId" label="用户" rules={[{ required: true }]}>
            {users.length > 0
              ? <Select showSearch optionFilterProp="label" placeholder="选择用户"
                  options={users.map((u) => ({ value: u.userId, label: `${u.displayName} (${u.userName})` }))} />
              : <Input placeholder="输入用户 ID" />}
          </Form.Item>
          <Form.Item name="allow" label="允许操作" rules={[{ required: true }]} initialValue={['SELECT']}>
            <Checkbox.Group options={[
              { label: 'SELECT', value: 'SELECT' },
              { label: 'INSERT', value: 'INSERT' },
              { label: 'UPDATE', value: 'UPDATE' },
              { label: 'DELETE', value: 'DELETE' },
            ]} />
          </Form.Item>
        </Form>
      </Modal>
    </Card>
  );
};

export default UserGrants;
