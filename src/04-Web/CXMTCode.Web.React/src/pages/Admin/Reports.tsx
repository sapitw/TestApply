import React, { useState } from 'react';
import { Button, Card, DatePicker, Space, message, Alert } from 'antd';
import { DownloadOutlined, FilePdfOutlined, FileTextOutlined } from '@ant-design/icons';
import dayjs from 'dayjs';
import { useAuthStore } from '../../stores/authStore';

const { RangePicker } = DatePicker;

const Reports: React.FC = () => {
  const token = useAuthStore((s) => s.token);
  const [range, setRange] = useState<[dayjs.Dayjs, dayjs.Dayjs]>(() => [
    dayjs().subtract(30, 'day').startOf('day'),
    dayjs().endOf('day'),
  ]);
  const [downloading, setDownloading] = useState(false);

  const downloadReport = async (format: 'html' | 'pdf') => {
    if (!range || !range[0] || !range[1]) { message.error('请选择时间区间'); return; }
    setDownloading(true);
    try {
      const url = `/api/audit/compliance-report/${format}?from=${range[0].toISOString()}&to=${range[1].toISOString()}`;
      const resp = await fetch(url, { headers: { Authorization: `Bearer ${token}` } });
      if (!resp.ok) { message.error(`下载失败：HTTP ${resp.status}`); return; }
      const blob = await resp.blob();
      const a = document.createElement('a');
      a.href = URL.createObjectURL(blob);
      a.download = `compliance-${range[0].format('YYYYMMDD')}-${range[1].format('YYYYMMDD')}.${format}`;
      a.click();
      URL.revokeObjectURL(a.href);
      message.success(`${format.toUpperCase()} 已下载`);
    } finally {
      setDownloading(false);
    }
  };

  return (
    <Card title="自检报告（SysAdmin 专属）">
      <Alert
        type="info"
        showIcon
        style={{ marginBottom: 16 }}
        message="21CFR Part11 合规自检报告"
        description="包含报告期内全部操作统计、按操作类型/结果分布、SM3 哈希链抽样验证、明细列表。建议每月归档一份。"
      />
      <Space direction="vertical" size="large" style={{ width: '100%' }}>
        <Space>
          <span>报告期：</span>
          <RangePicker
            showTime
            value={range}
            onChange={(v) => v && setRange(v as [dayjs.Dayjs, dayjs.Dayjs])}
          />
        </Space>
        <Space>
          <Button
            type="primary"
            icon={<FileTextOutlined />}
            loading={downloading}
            onClick={() => downloadReport('html')}
          >
            下载 HTML 报告
          </Button>
          <Button
            danger
            icon={<FilePdfOutlined />}
            loading={downloading}
            onClick={() => downloadReport('pdf')}
          >
            下载 PDF 报告
          </Button>
        </Space>
      </Space>
    </Card>
  );
};

export default Reports;
