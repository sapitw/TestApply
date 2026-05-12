import { create } from 'zustand';
import type { SystemEnvironment } from '../types/common';

interface EnvironmentState {
  current: SystemEnvironment;
  switching: boolean;
  setEnvironment: (env: SystemEnvironment) => void;
  startSwitching: () => void;
  endSwitching: () => void;
}

export const useEnvironmentStore = create<EnvironmentState>((set) => ({
  current: (localStorage.getItem('cxmtcode-env') as SystemEnvironment) || 'PROD',
  switching: false,
  setEnvironment: (env) => {
    localStorage.setItem('cxmtcode-env', env);
    set({ current: env });
  },
  startSwitching: () => set({ switching: true }),
  endSwitching: () => set({ switching: false }),
}));
