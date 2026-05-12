import React from 'react';
import { Card, Descriptions, Tag } from 'antd';
import { useAuthStore } from '../../stores/authStore';
import { UserRoleColor, UserRoleLabel } from '../../types/user';

const Profile: React.FC = () => {
  const user = useAuthStore((s) => s.user);
  if (!user) return null;
  return (
    <Card title="个人资料">
      <Descriptions column={1} labelStyle={{ width: 120 }} bordered>
        <Descriptions.Item label="账号">{user.userName}</Descriptions.Item>
        <Descriptions.Item label="姓名">{user.displayName}</Descriptions.Item>
        <Descriptions.Item label="角色">
          <Tag color={UserRoleColor[user.role]}>{UserRoleLabel[user.role]}</Tag>
        </Descriptions.Item>
        <Descriptions.Item label="部门">
          {user.departmentName || '-'} ({user.departmentCode || '-'})
        </Descriptions.Item>
        <Descriptions.Item label="邮箱">{user.email || '-'}</Descriptions.Item>
        <Descriptions.Item label="状态">
          {user.isActive ? <Tag color="green">启用</Tag> : <Tag color="red">已禁用</Tag>}
        </Descriptions.Item>
      </Descriptions>
    </Card>
  );
};

export default Profile;
