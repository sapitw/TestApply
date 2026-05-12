import React from 'react';
import { Result, Button } from 'antd';
import { Link } from 'react-router-dom';

const Forbidden: React.FC = () => (
  <Result
    status="403"
    title="403"
    subTitle="抱歉，您没有权限访问该页面"
    extra={<Button type="primary"><Link to="/dashboard">回到工作台</Link></Button>}
  />
);

export default Forbidden;
