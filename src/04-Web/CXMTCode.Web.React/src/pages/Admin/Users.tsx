import React, { useEffect, useState } from 'react';
import { Button, Card, Form, Input, Modal, Select, Switch, Table, Tag, message } from 'antd';
import { adminService, type CreateUserDto } from '../../services/adminService';
import { UserRole, UserRoleColor, UserRoleLabel, type UserInfo } from '../../types/user';

const Users: React.FC = () => {
  const [rows, setRows] = useState<UserInfo[]>([]);
  const [loading, setLoading] = useState(false);
  const [modal, setModal] = useState(false);
  const [form] = Form.useForm<CreateUserDto>();

  const load = async () => {
    setLoading(true);
    const r = await adminService.listUsers();
    if (r.success && r.data) setRows(r.data);
    setLoading(false);
  };
  useEffect(() => { load(); }, []);

  const submit = async () => {
    const v = await form.validateFields();
    const r = await adminService.createUser(v);
    if (r.success) { message.success('已新增用户'); setModal(false); form.resetFields(); load(); }
  };

  const updateRole = async (id: string, role: UserRole) => {
    await adminService.updateRole(id, role);
    message.success('角色已更新');
    load();
  };

  const toggle = async (id: string, active: boolean) => {
    await adminService.toggleActive(id, active);
    message.success(active ? '已启用' : '已禁用');
    load();
  };

  return (
    <Card title="用户与角色管理（SysAdmin 专属）" extra={<Button type="primary" onClick={() => setModal(true)}>新增用户</Button>}>
      <Table
        rowKey="userId"
        loading={loading}
        dataSource={rows}
        size="small"
        columns={[
          { title: '账号', dataIndex: 'userName' },
          { title: '姓名', dataIndex: 'displayName' },
          { title: '邮箱', dataIndex: 'email' },
          { title: '部门', dataIndex: 'departmentName' },
          { title: '角色', dataIndex: 'role', render: (r: UserRole, row: UserInfo) => (
            <Select size="small" value={r} style={{ width: 130 }}
              onChange={(nv) => updateRole(row.userId, nv)}
              options={Object.values(UserRole).map((v) => ({ value: v, label: UserRoleLabel[v] }))}
            />
          ) },
          { title: '状态', dataIndex: 'isActive',
            render: (a: boolean, r: UserInfo) => (
              <Switch checked={a} checkedChildren="启用" unCheckedChildren="禁用" onChange={(v) => toggle(r.userId, v)} />
            ) },
        ]}
      />
      <Modal title="新增用户" open={modal} onCancel={() => setModal(false)} onOk={submit} destroyOnClose>
        <Form form={form} layout="vertical">
          <Form.Item name="userName" label="账号" rules={[{ required: true }]}><Input /></Form.Item>
          <Form.Item name="password" label="初始密码" rules={[{ required: true, min: 8 }]}><Input.Password /></Form.Item>
          <Form.Item name="displayName" label="姓名" rules={[{ required: true }]}><Input /></Form.Item>
          <Form.Item name="email" label="邮箱"><Input /></Form.Item>
          <Form.Item name="departmentCode" label="部门编码"><Input /></Form.Item>
          <Form.Item name="departmentName" label="部门名称"><Input /></Form.Item>
          <Form.Item name="role" label="角色" rules={[{ required: true }]}>
            <Select options={Object.values(UserRole).map((v) => ({ value: v, label: UserRoleLabel[v] }))} />
          </Form.Item>
        </Form>
      </Modal>
    </Card>
  );
};

export default Users;
