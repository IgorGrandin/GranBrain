export type GenerativeUiBlockType =
  | 'kpi-grid'
  | 'habit-checklist'
  | 'kanban-board'
  | 'dynamic-form'
  | 'markdown-note-list';

export interface KpiMetricItem {
  label: string;
  value: string;
  subtitle?: string;
  accent?: string;
}

export interface HabitItem {
  time: string;
  label: string;
  fullText: string;
  completed: boolean;
}

export interface KanbanTask {
  id: string;
  text: string;
  completed: boolean;
  column: string;
}

export interface KanbanColumn {
  name: string;
  tasks: KanbanTask[];
}

export interface KanbanBoard {
  boardName: string;
  relativePath: string;
  columns: KanbanColumn[];
}

export interface DynamicFormField {
  key: string;
  label: string;
  fieldType: 'text' | 'textarea' | 'select' | 'date' | string;
  required?: boolean;
  placeholder?: string;
  defaultValue?: string;
  options?: string[];
}

export interface DynamicFormSchema {
  formId: string;
  noteType: string;
  submitLabel: string;
  fields: DynamicFormField[];
}

export interface VaultNoteSummary {
  title: string;
  relativePath: string;
  category: string;
  lastModifiedUtc: string;
  tags: string[];
  excerpt: string;
}

export interface GenerativeUiBlock {
  id: string;
  type: GenerativeUiBlockType | string;
  title: string;
  subtitle?: string;
  metrics?: KpiMetricItem[];
  habits?: HabitItem[];
  kanban?: KanbanBoard;
  form?: DynamicFormSchema;
  notes?: VaultNoteSummary[];
}

export interface AiProviderStatus {
  id: string;
  displayName: string;
  planName: string;
  executablePath: string;
  installed: boolean;
  description: string;
}

export interface AgentChatRequest {
  prompt: string;
  provider: string;
  voiceMode?: boolean;
}

export interface AgentResponseEnvelope {
  providerUsed: string;
  executedViaCli: boolean;
  replyText: string;
  touchedFiles: string[];
  uiBlocks: GenerativeUiBlock[];
  timestampUtc: string;
}

export interface LoginRequest {
  username: string;
  password: string;
}

export interface LoginResponse {
  token: string;
  username: string;
  expiresAtUtc: string;
}

export interface VaultOverview {
  vaultPath: string;
  todayNotePath: string;
  totalZettelkastenNotes: number;
  totalDailyNotes: number;
  totalIndices: number;
  inboxPendingCount: number;
  habitsCompletedToday: number;
  habitsTotalToday: number;
  todayHabits: HabitItem[];
  kanbanBoards: KanbanBoard[];
  recentNotes: VaultNoteSummary[];
}
