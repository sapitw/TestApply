import React, { useEffect, useState } from 'react';
import { Card, Descriptions, Spin, Tag } from 'antd';
import { useParams } from 'react-router-dom';
import { changeService } from '../../services/changeService';
import type { ChangeRequestDto } from '../../types/change';
import {
  ChangeRequestStatusColor,
  ChangeRequestStatusLabel,
  DatabaseTypeLabel,
  SqlOperationLabel,
} from '../../types/common';

const ChangeDetail: React.FC = () => {
  const { id } = useParams<{ id: string }>();
  const [data, setData] = useState<ChangeRequestDto | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    if (!id) return;
    changeService.get(id).then((r) => {
      if (r.success && r.data) setData(r.data);
      setLoading(false);
    });
  }, [id]);

  if (loading) return <Spin />;
  if (!data) return <Card>变更申请不存在</Card>;

  return (
    <Card title={`变更申请 ${data.requestId}`}>
      <Descriptions column={2} bordered size="small">
        <Descriptions.Item label="状态">
          <Tag color={ChangeRequestStatusColor[data.status]}>{ChangeRequestStatusLabel[data.status]}</Tag>
        </Descriptions.Item>
        <Descriptions.Item label="DB 类型">{DatabaseTypeLabel[data.databaseType]}</Descriptions.Item>
        <Descriptions.Item label="操作类型">{SqlOperationLabel[data.operationType]}</Descriptions.Item>
        <Descriptions.Item label="影响等级">{data.impactLevel}</Descriptions.Item>
        <Descriptions.Item label="目标表" span={2}>{data.targetTable}</Descriptions.Item>
        <Descriptions.Item label="申请原因" span={2}>{data.reason}</Descriptions.Item>
        <Descriptions.Item label="SQL 语句" span={2}>
          <pre style={{ background: 'rgba(255,255,255,0.05)', padding: 12, borderRadius: 4 }}>{data.sqlStatement}</pre>
        </Descriptions.Item>
        {data.dryRunResult && (
          <Descriptions.Item label="DryRun 结果" span={2}>
            <pre style={{ fontSize: 12 }}>{data.dryRunResult}</pre>
          </Descriptions.Item>
        )}
        {data.execResult && (
          <Descriptions.Item label="执行结果" span={2}>
            <pre style={{ fontSize: 12 }}>{data.execResult}</pre>
          </Descriptions.Item>
        )}
        <Descriptions.Item label="申请人">{data.applicantId}</Descriptions.Item>
        <Descriptions.Item label="审批人">{data.approverId || '-'}</Descriptions.Item>
        <Descriptions.Item label="创建时间">{data.createdAt}</Descriptions.Item>
        <Descriptions.Item label="完成时间">{data.completedAt || '-'}</Descriptions.Item>
      </Descriptions>
    </Card>
  );
};

export default ChangeDetail;
