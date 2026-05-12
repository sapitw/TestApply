import { theme as antTheme, type ThemeConfig } from 'antd';

/** CXMT 深蓝科技风格 - 参考 cxmt.com */
export const CXMTPalette = {
  bg: {
    primary: '#070d19',
    card: 'rgba(13, 27, 60, 0.85)',
    hover: 'rgba(30, 60, 120, 0.5)',
    sidebar: '#0a1428',
    header: 'rgba(7, 13, 25, 0.95)',
    input: 'rgba(10, 20, 40, 0.8)',
  },
  accent: {
    primary: '#0088ff',
    hover: '#33a3ff',
    active: '#0066cc',
    glow: 'rgba(0, 136, 255, 0.4)',
    gradient: 'linear-gradient(135deg, #0088ff 0%, #0055cc 100%)',
  },
  text: {
    primary: '#ffffff',
    secondary: 'rgba(255,255,255,0.8)',
    muted: 'rgba(255,255,255,0.5)',
    disabled: 'rgba(255,255,255,0.25)',
  },
  border: {
    default: 'rgba(255,255,255,0.08)',
    hover: 'rgba(0,136,255,0.3)',
    focus: '#0088ff',
  },
  status: {
    success: '#00d4aa',
    warning: '#ffaa00',
    error: '#ff4466',
    info: '#0088ff',
  },
  environment: {
    prod: {
      bg: 'linear-gradient(90deg, #1a0000 0%, #4d0000 50%, #1a0000 100%)',
      border: '#ff3333',
      text: '#ff6666',
    },
    test: {
      bg: 'linear-gradient(90deg, #001a00 0%, #004d00 50%, #001a00 100%)',
      border: '#33ff33',
      text: '#66ff66',
    },
  },
};

export const antdThemeConfig: ThemeConfig = {
  algorithm: antTheme.darkAlgorithm,
  token: {
    colorPrimary: CXMTPalette.accent.primary,
    colorSuccess: CXMTPalette.status.success,
    colorWarning: CXMTPalette.status.warning,
    colorError: CXMTPalette.status.error,
    colorInfo: CXMTPalette.status.info,
    colorBgBase: CXMTPalette.bg.primary,
    colorTextBase: CXMTPalette.text.primary,
    borderRadius: 8,
    fontSize: 14,
    fontFamily: "'Noto Sans SC', 'PingFang SC', 'Microsoft YaHei', sans-serif",
  },
  components: {
    Card: { colorBgContainer: CXMTPalette.bg.card },
    Table: {
      headerBg: 'rgba(0, 136, 255, 0.1)',
      headerColor: '#ffffff',
      rowHoverBg: 'rgba(0, 136, 255, 0.08)',
    },
    Menu: {
      darkItemBg: CXMTPalette.bg.sidebar,
      darkItemSelectedBg: 'rgba(0, 136, 255, 0.18)',
      darkItemHoverBg: 'rgba(0, 136, 255, 0.10)',
    },
    Layout: { headerBg: CXMTPalette.bg.header, siderBg: CXMTPalette.bg.sidebar },
    Modal: { contentBg: 'rgba(16, 32, 68, 0.98)', headerBg: 'rgba(0, 136, 255, 0.05)' },
  },
};
