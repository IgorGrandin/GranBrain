import { Injectable } from '@angular/core';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Observable, tap } from 'rxjs';
import {
  AgentChatRequest,
  AgentResponseEnvelope,
  AiProviderStatus,
  HabitItem,
  KanbanBoard,
  LoginRequest,
  LoginResponse,
  VaultNoteSummary,
  VaultOverview
} from '../models/granbrain.models';

@Injectable({
  providedIn: 'root'
})
export class GranBrainApiService {
  private readonly baseUrl = '/api';
  private tokenKey = 'granbrain_token';

  constructor(private http: HttpClient) {}

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
}
