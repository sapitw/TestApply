import React from 'react';
import { Alert, Card } from 'antd';

const Reports: React.FC = () => (
  <Card title="自检报告（SysAdmin 专属）">
    <Alert
      type="info"
      showIcon
      message="TODO：自检报告导出"
      description="导出 21CFR Part11 合规自检报告 PDF / HTML。后端 E4 ComplianceReportPlugin 已就位，前端 UI 待接入文件下载流。"
    />
  </Card>
);

export default Reports;
