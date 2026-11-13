export type GenerativeUiBlockType =
  | 'kpi-grid'
  | 'habit-checklist'
  | 'kanban-board'
  | 'dynamic-form'
  | 'markdown-note-list'
  | 'idea-capture'
  | 'brainstorm-canvas'
  | 'agenda-timeline';

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
  ideasOverview?: IdeasOverview;
  calendarAgenda?: CalendarToday;
}

export type CalendarProvider = 'Google' | 'Teams' | 'Outlook';

export interface CalendarEvent {
  id: string;
  provider: CalendarProvider;
  title: string;
  startIso: string;
  endIso: string;
  startTime: string;
  endTime: string;
  durationMinutes: number;
  location?: string | null;
  attendees: string[];
  joinUrl?: string | null;
  notePath?: string | null;
  status: 'past' | 'now' | 'next' | string;
  hasConflict: boolean;
  calendarTopPx: number;
  calendarHeightPx: number;
  calendarColumn: number;
  calendarTotalColumns: number;
}

export interface CalendarToday {
  date: string;
  timeZone: string;
  lastSyncedAt?: string | null;
  conflictsCount: number;
  providersConnected: string[];
  events: CalendarEvent[];
}

export interface CalendarSyncResult {
  date: string;
  dailyNotePath: string;
  syncedCount: number;
  conflictsCount: number;
  createdNotes: string[];
  agenda: CalendarToday;
}

export interface CalendarNoteResult {
  eventId: string;
  title: string;
  notePath: string;
  created: boolean;
}

export interface IdeaItem {
  id: string;
  text: string;
  time: string;
  status: 'fleeting' | 'brainstorm' | 'promovida' | 'descartada' | string;
  sourcePath: string;
  links?: string[];
}

export interface BrainstormHypothesis {
  text: string;
  confidence: 'alta' | 'média' | 'baixa' | string;
}

export interface BrainstormRisk {
  text: string;
  severity: 'alto' | 'médio' | 'baixo' | string;
}

export interface BrainstormSession {
  idea: IdeaItem;
  notePath: string;
  problem: string;
  hypotheses: BrainstormHypothesis[];
  risks: BrainstormRisk[];
  socraticQuestion: string;
  socraticAnswer?: string;
}

export interface IdeasOverview {
  ideas: IdeaItem[];
  fleetingCount: number;
  brainstormCount: number;
  promotedCount: number;
  discardedCount: number;
  activeSession?: BrainstormSession | null;
}

export interface FleetingIdeaCaptureRequest {
  text: string;
  target?: string;
  links?: string[];
  tags?: string[];
}

export interface BrainstormStartRequest {
  ideaId?: string;
  ideaText?: string;
  problem?: string;
  hypotheses?: BrainstormHypothesis[];
  risks?: BrainstormRisk[];
  socraticQuestion?: string;
  project?: string;
}

export interface IdeaPromoteRequest {
  ideaId: string;
  target: 'permanente' | 'projeto' | 'descartada' | string;
  summary?: string;
  area?: string;
  socraticAnswer?: string;
}

export interface IdeaPromotionResult {
  idea: IdeaItem;
  originPath: string;
  targetPath?: string;
  status: string;
}

export interface AiProviderStatus {
  id: string;
  displayName: string;
  planName: string;
  model: string;
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
