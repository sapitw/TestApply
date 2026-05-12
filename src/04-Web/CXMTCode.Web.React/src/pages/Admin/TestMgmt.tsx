import React from 'react';
import { Alert, Card } from 'antd';

const TestMgmt: React.FC = () => (
  <Card title="测试管理（SysAdmin 专属）">
    <Alert
      type="info"
      showIcon
      message="F1-F5 测试引擎控制台 - TODO"
      description={
        <div>
          <p>测试引擎相关功能（已在后端 <code>CXMTCode.Plugins.TestEngine</code> 项目内实现接口占位）：</p>
          <ul>
            <li>F1 自动测试用例编排 - <code>AutoTestOrchestratorPlugin</code></li>
            <li>F2 测试门禁 - <code>TestGatePlugin</code>，可配置最低通过率阈值</li>
            <li>F3 回归测试 - <code>RegressionTestPlugin</code></li>
            <li>F4 性能测试 - <code>PerformanceTestPlugin</code></li>
            <li>F5 测试结果聚合 - <code>TestReportAggregatorPlugin</code></li>
          </ul>
          <p>后续需要补充：测试套件 / 用例 / 执行计划三张表的 CRUD 接口，以及测试执行的实时 SSE 推送。</p>
        </div>
      }
    />
  </Card>
);

export default TestMgmt;
