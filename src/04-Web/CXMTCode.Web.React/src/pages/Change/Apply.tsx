import React, { useEffect, useState } from 'react';
import { Card, Form, Input, Select, Button, message, Alert, InputNumber, Result } from 'antd';
import { useNavigate } from 'react-router-dom';
import { DatabaseType, DatabaseTypeLabel, SqlOperationType, SqlOperationLabel } from '../../types/common';
import { dbConnectionService } from '../../services/dbConnectionService';
import { changeService } from '../../services/changeService';
import type { DbConnectionDto } from '../../types/dbConnection';
import { useEnvironmentStore } from '../../stores/environmentStore';

const ChangeApply: React.FC = () => {
  const navigate = useNavigate();
  const [form] = Form.useForm();
  const [connections, setConnections] = useState<DbConnectionDto[]>([]);
  const [submitting, setSubmitting] = useState(false);
  const [rejected, setRejected] = useState<string | null>(null);
  const env = useEnvironmentStore((s) => s.current);

  useEffect(() => {
    dbConnectionService.list().then((r) => {
      if (r.success && r.data) setConnections(r.data.filter((c) => c.environmentType === env));
    });
  }, [env]);

  const onFinish = async (v: any) => {
    setSubmitting(true);
    setRejected(null);
    try {
      const r = await changeService.submit({
        operationType: v.operationType,
        databaseType: v.databaseType,
        connectionId: v.connectionId,
        targetTable: v.targetTable,
        sqlStatement: v.sqlStatement,
        reason: v.reason,
        impactLevel: v.impactLevel ?? 1,
      });
      if (r.success && r.data) {
        if (r.data.success) {
          message.success(`提交成功 ${r.data.requestId}`);
          navigate('/change/list');
        } else {
          setRejected(r.data.rejectReason || '提交被拒');
        }
      }
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <Card title={`变更申请 · 当前环境：${env}`}>
      <Alert
        message="安全提示"
        description="提交前会依次进行 10 条硬编码安全红线 + AST 语义校验。任何一项失败将立即拦截并记录审计。"
        type="info"
        showIcon
        style={{ marginBottom: 16 }}
      />
      {rejected && <Alert type="error" showIcon closable message={rejected} onClose={() => setRejected(null)} style={{ marginBottom: 16 }} />}

      <Form form={form} layout="vertical" onFinish={onFinish}>
        <Form.Item label="目标数据库连接" name="connectionId" rules={[{ required: true }]}>
          <Select placeholder={`选择已配置的 ${env} 连接`} options={connections.map((c) => ({
            label: `${c.connectionName} (${DatabaseTypeLabel[c.databaseType]})`, value: c.connectionId,
          }))} />
        </Form.Item>

        <Form.Item label="数据库类型" name="databaseType" rules={[{ required: true }]}>
          <Select options={Object.entries(DatabaseTypeLabel).map(([v, l]) => ({ value: Number(v), label: l }))} />
        </Form.Item>

        <Form.Item label="操作类型" name="operationType" rules={[{ required: true }]} initialValue={SqlOperationType.Update}>
          <Select options={Object.entries(SqlOperationLabel).map(([v, l]) => ({ value: Number(v), label: l }))} />
        </Form.Item>

        <Form.Item label="目标表" name="targetTable" rules={[{ required: true }]}>
          <Input placeholder="如 PROD_SCHEMA.WAFER_INFO" />
        </Form.Item>

        <Form.Item label="变更 SQL" name="sqlStatement" rules={[{ required: true }]}>
          <Input.TextArea
            rows={8}
            placeholder="-- 必须带 WHERE 条件；禁止 DROP / TRUNCATE / GRANT / UNION 等高危操作"
            style={{ fontFamily: 'JetBrains Mono, monospace' }}
          />
        </Form.Item>

        <Form.Item label="申请原因" name="reason" rules={[{ required: true, min: 10, message: '至少 10 个字符' }]}>
          <Input.TextArea rows={3} placeholder="详细说明此次变更的业务背景与必要性" />
        </Form.Item>

        <Form.Item label="影响等级" name="impactLevel" initialValue={2}>
          <InputNumber min={1} max={4} addonAfter="1=低 2=中 3=高 4=极高" />
        </Form.Item>

        <Button type="primary" htmlType="submit" loading={submitting}>提交变更申请</Button>
      </Form>
    </Card>
  );
};

export default ChangeApply;
