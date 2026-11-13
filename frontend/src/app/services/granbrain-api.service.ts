import { Injectable } from '@angular/core';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Observable, tap } from 'rxjs';
import {
  AgentChatRequest,
  AgentResponseEnvelope,
  AiProviderStatus,
  BrainstormSession,
  BrainstormStartRequest,
  CalendarNoteResult,
  CalendarSyncResult,
  CalendarToday,
  FleetingIdeaCaptureRequest,
  GranBrainRuntimeConfig,
  HabitItem,
  IdeaItem,
  IdeaPromoteRequest,
  IdeaPromotionResult,
  IdeasOverview,
  KanbanBoard,
  LoginRequest,
  LoginResponse,
  RoutineRunRequest,
  RoutineRunResult,
  RoutinesOverview,
  SystemHealthStatus,
  VaultNoteSummary,
  VaultOverview
} from '../models/granbrain.models';

declare global {
  interface Window {
    __GRANBRAIN_CONFIG__?: GranBrainRuntimeConfig;
  }
}

@Injectable({
  providedIn: 'root'
})
export class GranBrainApiService {
  private tokenKey = 'granbrain_token';

  constructor(private http: HttpClient) {}

  public static resolveApiBaseUrl(rawUrl?: string | null): string {
    const trimmed = (rawUrl ?? '').trim().replace(/\/+$/, '');
    if (!trimmed) {
      return '/api';
    }
    return trimmed.endsWith('/api') ? trimmed : `${trimmed}/api`;
  }

  public resolveApiBaseUrl(rawUrl?: string | null): string {
    return GranBrainApiService.resolveApiBaseUrl(rawUrl);
  }

  private get baseUrl(): string {
    const configured = typeof window !== 'undefined' ? window.__GRANBRAIN_CONFIG__?.apiBaseUrl : undefined;
    return GranBrainApiService.resolveApiBaseUrl(configured);
  }

  public getHealth(): Observable<SystemHealthStatus> {
    return this.http.get<SystemHealthStatus>(`${this.baseUrl}/health`);
  }

  public getToken(): string | null {
    try {
      return localStorage.getItem(this.tokenKey);
    } catch {
      return null;
    }
  }

  public setToken(token: string): void {
    try {
      localStorage.setItem(this.tokenKey, token);
    } catch {
      // Ignore if localStorage unavailable
    }
  }

  public clearToken(): void {
    try {
      localStorage.removeItem(this.tokenKey);
    } catch {
      // Ignore
    }
  }

  public get isAuthenticated(): boolean {
    return !!this.getToken();
  }

  private getAuthHeaders(): HttpHeaders {
    const token = this.getToken();
    let headers = new HttpHeaders();
    if (token) {
      headers = headers.set('Authorization', `Bearer ${token}`);
    }
    return headers;
  }

  public login(req: LoginRequest): Observable<LoginResponse> {
    return this.http.post<LoginResponse>(`${this.baseUrl}/auth/login`, req).pipe(
      tap((res) => {
        if (res && res.token) {
          this.setToken(res.token);
        }
      })
    );
  }

  public getProviders(): Observable<AiProviderStatus[]> {
    return this.http.get<AiProviderStatus[]>(`${this.baseUrl}/agent/providers`, {
      headers: this.getAuthHeaders()
    });
  }

  public sendChat(prompt: string, provider: string, voiceMode = false): Observable<AgentResponseEnvelope> {
    const payload: AgentChatRequest = { prompt, provider, voiceMode };
    return this.http.post<AgentResponseEnvelope>(`${this.baseUrl}/agent/chat`, payload, {
      headers: this.getAuthHeaders()
    });
  }

  public getOverview(): Observable<VaultOverview> {
    return this.http.get<VaultOverview>(`${this.baseUrl}/vault/overview`, {
      headers: this.getAuthHeaders()
    });
  }

  public toggleHabit(habitQuery: string, date?: string): Observable<HabitItem[]> {
    return this.http.post<HabitItem[]>(
      `${this.baseUrl}/vault/habits/toggle`,
      { habitQuery, date },
      { headers: this.getAuthHeaders() }
    );
  }

  public getKanban(board: string): Observable<KanbanBoard> {
    return this.http.get<KanbanBoard>(`${this.baseUrl}/vault/kanban/${encodeURIComponent(board)}`, {
      headers: this.getAuthHeaders()
    });
  }

  public addKanbanTask(board: string, column: string, taskText: string): Observable<KanbanBoard> {
    return this.http.post<KanbanBoard>(
      `${this.baseUrl}/vault/kanban/${encodeURIComponent(board)}/tasks`,
      { column, taskText },
      { headers: this.getAuthHeaders() }
    );
  }

  public moveKanbanTask(board: string, taskText: string, targetColumn: string): Observable<KanbanBoard> {
    return this.http.put<KanbanBoard>(
      `${this.baseUrl}/vault/kanban/${encodeURIComponent(board)}/tasks/move`,
      { taskText, targetColumn },
      { headers: this.getAuthHeaders() }
    );
  }

  public createNote(note: {
    noteType: string;
    title: string;
    content: string;
    area?: string;
    tags?: string[];
    extraFields?: Record<string, string>;
  }): Observable<VaultNoteSummary> {
    return this.http.post<VaultNoteSummary>(`${this.baseUrl}/vault/notes`, note, {
      headers: this.getAuthHeaders()
    });
  }

  public queryNotes(query?: string, category?: string, limit = 20): Observable<VaultNoteSummary[]> {
    let url = `${this.baseUrl}/vault/notes?limit=${limit}`;
    if (query) url += `&query=${encodeURIComponent(query)}`;
    if (category) url += `&category=${encodeURIComponent(category)}`;
    return this.http.get<VaultNoteSummary[]>(url, { headers: this.getAuthHeaders() });
  }

  public bootstrapVault(): Observable<{ success: boolean; message: string }> {
    return this.http.post<{ success: boolean; message: string }>(
      `${this.baseUrl}/vault/bootstrap`,
      {},
      { headers: this.getAuthHeaders() }
    );
  }

  public getIdeasOverview(): Observable<IdeasOverview> {
    return this.http.get<IdeasOverview>(`${this.baseUrl}/vault/ideas/overview`, {
      headers: this.getAuthHeaders()
    });
  }

  public captureFleetingIdea(req: FleetingIdeaCaptureRequest): Observable<IdeaItem> {
    return this.http.post<IdeaItem>(`${this.baseUrl}/vault/ideas/fleeting`, req, {
      headers: this.getAuthHeaders()
    });
  }

  public startBrainstorm(req: BrainstormStartRequest): Observable<BrainstormSession> {
    return this.http.post<BrainstormSession>(`${this.baseUrl}/vault/ideas/brainstorm`, req, {
      headers: this.getAuthHeaders()
    });
  }

  public promoteIdea(req: IdeaPromoteRequest): Observable<IdeaPromotionResult> {
    return this.http.post<IdeaPromotionResult>(`${this.baseUrl}/vault/ideas/promote`, req, {
      headers: this.getAuthHeaders()
    });
  }

  public getCalendarToday(date?: string): Observable<CalendarToday> {
    const query = date ? `?date=${encodeURIComponent(date)}` : '';
    return this.http.get<CalendarToday>(`${this.baseUrl}/calendar/today${query}`, {
      headers: this.getAuthHeaders()
    });
  }

  public syncCalendar(date?: string, targetFolder?: string): Observable<CalendarSyncResult> {
    const body: { date?: string; targetFolder?: string } = {};
    if (date) body.date = date;
    if (targetFolder) body.targetFolder = targetFolder;
    return this.http.post<CalendarSyncResult>(
      `${this.baseUrl}/calendar/sync`,
      body,
      { headers: this.getAuthHeaders() }
    );
  }

  public createCalendarEventNote(eventId: string, targetFolder?: string, date?: string): Observable<CalendarNoteResult> {
    const body: { targetFolder?: string; date?: string } = {};
    if (targetFolder) body.targetFolder = targetFolder;
    if (date) body.date = date;
    return this.http.post<CalendarNoteResult>(
      `${this.baseUrl}/calendar/events/${encodeURIComponent(eventId)}/note`,
      body,
      { headers: this.getAuthHeaders() }
    );
  }

  public getRoutinesOverview(): Observable<RoutinesOverview> {
    return this.http.get<RoutinesOverview>(`${this.baseUrl}/routines/overview`, {
      headers: this.getAuthHeaders()
    });
  }

  public triageInboxNow(request: RoutineRunRequest = {}): Observable<RoutineRunResult> {
    return this.http.post<RoutineRunResult>(`${this.baseUrl}/routines/triage`, request, {
      headers: this.getAuthHeaders()
    });
  }

  public runRoutineJob(jobId: string, request: RoutineRunRequest = {}): Observable<RoutineRunResult> {
    return this.http.post<RoutineRunResult>(
      `${this.baseUrl}/routines/${encodeURIComponent(jobId)}/run`,
      request,
      { headers: this.getAuthHeaders() }
    );
  }
}
