import React from 'react';
import { ConfigProvider, App as AntApp } from 'antd';
import zhCN from 'antd/locale/zh_CN';
import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom';
import { antdThemeConfig } from './styles/theme';
import { UserRole } from './types/user';
import AppLayout from './layouts/AppLayout';
import RequireAuth from './routes/RequireAuth';
import LoginPage from './pages/Login';
import Dashboard from './pages/Dashboard';
import Profile from './pages/Profile';
import Forbidden from './pages/Forbidden';
import FeatureSummary from './pages/Feature';
import ChangeApply from './pages/Change/Apply';
import ChangeList from './pages/Change/List';
import ChangeDetail from './pages/Change/Detail';
import Rollback from './pages/Change/Rollback';
import AuditOwn from './pages/Audit/Own';
import AuditAll from './pages/Audit/All';
import DbConnections from './pages/Dba/DbConnections';
import TableAccess from './pages/Dba/TableAccess';
import Templates from './pages/Dba/Templates';
import Rules from './pages/Dba/Rules';
import UserGrants from './pages/Dba/UserGrants';
import Users from './pages/Admin/Users';
import TestMgmt from './pages/Admin/TestMgmt';
import Reports from './pages/Admin/Reports';
import Config from './pages/Admin/Config';

const App: React.FC = () => (
  <ConfigProvider locale={zhCN} theme={antdThemeConfig}>
    <AntApp>
      <BrowserRouter>
        <Routes>
          <Route path="/login" element={<LoginPage />} />
          <Route path="/forbidden" element={<Forbidden />} />

          <Route path="/" element={<RequireAuth><AppLayout /></RequireAuth>}>
            <Route index element={<Navigate to="/dashboard" replace />} />
            <Route path="dashboard" element={<Dashboard />} />
            <Route path="feature-summary" element={<FeatureSummary />} />
            <Route path="profile" element={<Profile />} />

            <Route path="change">
              <Route index element={<Navigate to="/change/list" replace />} />
              <Route path="apply" element={<ChangeApply />} />
              <Route path="list" element={<ChangeList />} />
              <Route path="detail/:id" element={<ChangeDetail />} />
              <Route path="rollback" element={<Rollback />} />
            </Route>

            <Route path="audit">
              <Route index element={<Navigate to="/audit/own" replace />} />
              <Route path="own" element={<AuditOwn />} />
              <Route path="all" element={<RequireAuth required={UserRole.DBA}><AuditAll /></RequireAuth>} />
            </Route>

            <Route path="dba" element={<RequireAuth required={UserRole.DBA}><Routes>
              <Route index element={<Navigate to="/dba/db-connections" replace />} />
            </Routes></RequireAuth>} />
            <Route path="dba/db-connections" element={<RequireAuth required={UserRole.DBA}><DbConnections /></RequireAuth>} />
            <Route path="dba/table-access"   element={<RequireAuth required={UserRole.DBA}><TableAccess /></RequireAuth>} />
            <Route path="dba/templates"      element={<RequireAuth required={UserRole.DBA}><Templates /></RequireAuth>} />
            <Route path="dba/rules"          element={<RequireAuth required={UserRole.DBA}><Rules /></RequireAuth>} />
            <Route path="dba/user-grants"    element={<RequireAuth required={UserRole.DBA}><UserGrants /></RequireAuth>} />

            <Route path="admin/users"     element={<RequireAuth required={UserRole.SysAdmin}><Users /></RequireAuth>} />
            <Route path="admin/test-mgmt" element={<RequireAuth required={UserRole.SysAdmin}><TestMgmt /></RequireAuth>} />
            <Route path="admin/reports"   element={<RequireAuth required={UserRole.SysAdmin}><Reports /></RequireAuth>} />
            <Route path="admin/config"    element={<RequireAuth required={UserRole.SysAdmin}><Config /></RequireAuth>} />
          </Route>

          <Route path="*" element={<Navigate to="/dashboard" replace />} />
        </Routes>
      </BrowserRouter>
    </AntApp>
  </ConfigProvider>
);

export default App;
