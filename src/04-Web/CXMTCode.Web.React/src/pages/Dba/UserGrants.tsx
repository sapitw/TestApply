import React from 'react';
import { Alert, Card } from 'antd';

const UserGrants: React.FC = () => (
  <Card title="用户表级授权（DBA 专属）">
    <Alert
      type="info"
      showIcon
      message="TODO：用户表级授权"
      description={
        <div>
          <p>需要后端补充以下接口才能完成完整界面：</p>
          <ul>
            <li>GET <code>/api/dba/user-grants?accessId=...</code> 按已准入表查询授权清单</li>
            <li>POST <code>/api/dba/user-grants</code> 给指定用户授予 SELECT/INSERT/UPDATE/DELETE 权限</li>
            <li>DELETE <code>/api/dba/user-grants/&#123;id&#125;</code> 取消授权</li>
          </ul>
          <p>当前页面留位以待后端 <code>TablePermission</code> 表 CRUD 完整接入。</p>
        </div>
      }
    />
  </Card>
);

export default UserGrants;
