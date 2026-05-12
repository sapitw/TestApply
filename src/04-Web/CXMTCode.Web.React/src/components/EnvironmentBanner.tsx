import React, { useEffect } from 'react';
import { Switch, Tag, Tooltip } from 'antd';
import { useEnvironmentStore } from '../stores/environmentStore';
import { useAuthStore } from '../stores/authStore';
import { dbConnectionService } from '../services/dbConnectionService';
import { UserRole } from '../types/user';

/** 顶部环境标识横幅 - 红=生产 / 绿=测试 / SysAdmin 可切换 */
export const EnvironmentBanner: React.FC = () => {
  const { current, switching, setEnvironment, startSwitching, endSwitching } = useEnvironmentStore();
  const { user } = useAuthStore();
  const isSysAdmin = user?.role === UserRole.SysAdmin;

  useEffect(() => {
    dbConnectionService.currentEnv().then((r) => {
      if (r.success && r.data) setEnvironment(r.data);
    });
  }, [setEnvironment]);

  const onSwitch = async (toTest: boolean) => {
    const target = toTest ? 'TEST' : 'PROD';
    if (target === current) return;
    startSwitching();
    try {
      const r = await dbConnectionService.switchEnv(target, 'SysAdmin 顶部切换');
      if (r.success) {
        setEnvironment(target);
        window.location.reload();
      }
    } finally {
      endSwitching();
    }
  };

  const isProd = current === 'PROD';
  return (
    <div className={`cxmt-banner ${isProd ? 'prod' : 'test'}`}>
      {isProd ? (
        <>
          <Tag color="red" style={{ fontWeight: 600 }}>⚠️ 生产环境 PRODUCTION</Tag>
          <span style={{ color: '#ff7875' }}>所有操作将直接影响生产数据库</span>
        </>
      ) : (
        <>
          <Tag color="green" style={{ fontWeight: 600 }}>✅ 测试环境 TEST</Tag>
          <span style={{ color: '#95de64' }}>操作仅在测试数据库执行，不影响生产</span>
        </>
      )}
      {isSysAdmin && (
        <Tooltip title="仅 SysAdmin 可切换全局环境">
          <span style={{ display: 'inline-flex', alignItems: 'center', gap: 6 }}>
            <span style={{ color: 'rgba(255,255,255,0.5)' }}>切换：</span>
            <Switch
              checked={!isProd}
              loading={switching}
              size="small"
              checkedChildren="TEST"
              unCheckedChildren="PROD"
              onChange={onSwitch}
            />
          </span>
        </Tooltip>
      )}
    </div>
  );
};

export default EnvironmentBanner;
