import React, { useEffect, useState } from 'react';
import { Button, Card, Input, Table, message } from 'antd';
import { adminService } from '../../services/adminService';

const Config: React.FC = () => {
  const [rows, setRows] = useState<{ key: string; value: string }[]>([]);
  const [loading, setLoading] = useState(false);

  const load = async () => {
    setLoading(true);
    const r = await adminService.listConfig();
    if (r.success && r.data)
      setRows(Object.entries(r.data).map(([key, value]) => ({ key, value })));
    setLoading(false);
  };
  useEffect(() => { load(); }, []);

  const update = async (key: string, value: string) => {
    await adminService.updateConfig(key, value);
    message.success('已保存');
    load();
  };

  return (
    <Card title="系统配置（SysAdmin 专属）">
      <Table
        rowKey="key"
        loading={loading}
        dataSource={rows}
        size="small"
        columns={[
          { title: '配置项', dataIndex: 'key', width: 280 },
          { title: '值', dataIndex: 'value',
            render: (v: string, r) => (
              <Input
                defaultValue={v}
                onBlur={(e) => e.target.value !== v && update(r.key, e.target.value)}
              />
            ) },
        ]}
      />
    </Card>
  );
};

export default Config;
