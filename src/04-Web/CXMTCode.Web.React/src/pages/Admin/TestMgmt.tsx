import React, { useEffect, useState } from 'react';
import { Button, Card, Form, Input, InputNumber, Modal, Switch, Table, Tag, message, Space, Tabs } from 'antd';
import { PlusOutlined, PlayCircleOutlined } from '@ant-design/icons';
import { testEngineService, type TestCaseDto, type TestRunDto, type TestSuiteDto } from '../../services/testEngineService';

const TestMgmt: React.FC = () => {
  const [suites, setSuites] = useState<TestSuiteDto[]>([]);
  const [cases, setCases] = useState<TestCaseDto[]>([]);
  const [runs, setRuns] = useState<TestRunDto[]>([]);
  const [selectedSuite, setSelectedSuite] = useState<string | undefined>();
  const [loading, setLoading] = useState(false);
  const [suiteModal, setSuiteModal] = useState(false);
  const [caseModal, setCaseModal] = useState(false);
  const [suiteForm] = Form.useForm();
  const [caseForm] = Form.useForm();

  const loadSuites = async () => {
    setLoading(true);
    const r = await testEngineService.listSuites();
    if (r.success && r.data) setSuites(r.data);
    setLoading(false);
  };
  const loadCases = async (id: string) => {
    const r = await testEngineService.listCases(id);
    if (r.success && r.data) setCases(r.data);
  };
  const loadRuns = async (id?: string) => {
    const r = await testEngineService.listRuns(id);
    if (r.success && r.data) setRuns(r.data);
  };

  useEffect(() => { loadSuites(); loadRuns(); }, []);
  useEffect(() => {
    if (selectedSuite) { loadCases(selectedSuite); loadRuns(selectedSuite); }
  }, [selectedSuite]);

  const createSuite = async () => {
    const v = await suiteForm.validateFields();
    const r = await testEngineService.createSuite(v);
    if (r.success) { message.success('已创建套件'); setSuiteModal(false); suiteForm.resetFields(); loadSuites(); }
  };
  const addCase = async () => {
    const v = await caseForm.validateFields();
    if (!selectedSuite) return;
    const r = await testEngineService.addCase(selectedSuite, { ...v, ordinal: v.ordinal ?? 0, enabled: true });
    if (r.success) { message.success('已添加用例'); setCaseModal(false); caseForm.resetFields(); loadCases(selectedSuite); }
  };
  const triggerRun = async (id: string) => {
    const r = await testEngineService.run(id);
    if (r.success && r.data) {
      message.success(`运行完成：${r.data.status}，通过率 ${(r.data.passRate * 100).toFixed(1)}%`);
      loadRuns(selectedSuite);
    }
  };

  return (
    <Card title="测试管理（SysAdmin 专属）" extra={<Button type="primary" icon={<PlusOutlined />} onClick={() => setSuiteModal(true)}>新增套件</Button>}>
      <Tabs
        items={[
          {
            key: 'suites',
            label: '测试套件',
            children: (
              <Table
                rowKey="suiteId"
                loading={loading}
                dataSource={suites}
                size="small"
                onRow={(r) => ({ onClick: () => setSelectedSuite(r.suiteId) })}
                rowClassName={(r) => r.suiteId === selectedSuite ? 'ant-table-row-selected' : ''}
                columns={[
                  { title: '套件名', dataIndex: 'suiteName' },
                  { title: '说明', dataIndex: 'description' },
                  { title: '门禁', dataIndex: 'isGate', render: (v: boolean) => v ? <Tag color="red">门禁</Tag> : <Tag>否</Tag> },
                  { title: '最低通过率', dataIndex: 'minPassRate', render: (v: number) => `${(v * 100).toFixed(1)}%` },
                  { title: '创建时间', dataIndex: 'createdAt' },
                  {
                    title: '操作', key: 'action',
                    render: (_: any, r: TestSuiteDto) => (
                      <Space>
                        <Button size="small" icon={<PlayCircleOutlined />} onClick={(e) => { e.stopPropagation(); triggerRun(r.suiteId); }}>运行</Button>
                      </Space>
                    ),
                  },
                ]}
              />
            ),
          },
          {
            key: 'cases',
            label: `用例 ${selectedSuite ? `(${cases.length})` : ''}`,
            disabled: !selectedSuite,
            children: (
              <>
                <Button style={{ marginBottom: 12 }} type="primary" onClick={() => setCaseModal(true)}>添加用例</Button>
                <Table
                  rowKey="caseId"
                  dataSource={cases}
                  size="small"
                  columns={[
                    { title: '名称', dataIndex: 'caseName' },
                    { title: '类型', dataIndex: 'caseType', render: (v: string) => <Tag color="blue">{v}</Tag> },
                    { title: 'SQL', dataIndex: 'targetSql', render: (v?: string) => v && <code style={{ fontSize: 11 }}>{v}</code> },
                    { title: '排序', dataIndex: 'ordinal', width: 80 },
                    { title: '启用', dataIndex: 'enabled', render: (v: boolean) => v ? <Tag color="green">是</Tag> : <Tag>否</Tag> },
                  ]}
                />
              </>
            ),
          },
          {
            key: 'runs',
            label: '执行历史',
            children: (
              <Table
                rowKey="runId"
                dataSource={runs}
                size="small"
                columns={[
                  { title: 'RunId', dataIndex: 'runId' },
                  { title: 'Suite', dataIndex: 'suiteId' },
                  { title: '状态', dataIndex: 'status',
                    render: (v: string) => <Tag color={v === 'PASSED' ? 'green' : v === 'FAILED' ? 'red' : 'gold'}>{v}</Tag> },
                  { title: '总数', dataIndex: 'totalCases' },
                  { title: '通过', dataIndex: 'passedCases' },
                  { title: '失败', dataIndex: 'failedCases' },
                  { title: '通过率', dataIndex: 'passRate', render: (v: number) => `${(v * 100).toFixed(1)}%` },
                  { title: '开始时间', dataIndex: 'startedAt' },
                ]}
              />
            ),
          },
        ]}
      />

      <Modal title="新增测试套件" open={suiteModal} onCancel={() => setSuiteModal(false)} onOk={createSuite} destroyOnClose>
        <Form form={suiteForm} layout="vertical">
          <Form.Item name="suiteName" label="套件名" rules={[{ required: true }]}><Input /></Form.Item>
          <Form.Item name="description" label="说明"><Input.TextArea rows={3} /></Form.Item>
          <Form.Item name="isGate" label="作为上线门禁" valuePropName="checked"><Switch /></Form.Item>
          <Form.Item name="minPassRate" label="最低通过率" initialValue={0.95}><InputNumber min={0} max={1} step={0.05} style={{ width: '100%' }} /></Form.Item>
        </Form>
      </Modal>

      <Modal title="添加测试用例" open={caseModal} onCancel={() => setCaseModal(false)} onOk={addCase} destroyOnClose>
        <Form form={caseForm} layout="vertical">
          <Form.Item name="caseName" label="用例名" rules={[{ required: true }]}><Input /></Form.Item>
          <Form.Item name="caseType" label="类型" initialValue="UNIT" rules={[{ required: true }]}>
            <Input placeholder="UNIT / INTEGRATION / PERFORMANCE" />
          </Form.Item>
          <Form.Item name="targetSql" label="目标 SQL"><Input.TextArea rows={3} /></Form.Item>
          <Form.Item name="expected" label="期望输出"><Input.TextArea rows={2} /></Form.Item>
          <Form.Item name="ordinal" label="排序" initialValue={0}><InputNumber min={0} style={{ width: '100%' }} /></Form.Item>
        </Form>
      </Modal>
    </Card>
  );
};

export default TestMgmt;
