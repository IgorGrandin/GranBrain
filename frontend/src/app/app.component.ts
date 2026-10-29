import { Component, OnDestroy, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Subscription } from 'rxjs';
import { GranBrainApiService } from './services/granbrain-api.service';
import { VoiceService } from './services/voice.service';
import {
  AiProviderStatus,
  GenerativeUiBlock,
  HabitItem,
  VaultNoteSummary
} from './models/granbrain.models';
import { GenerativeCanvasComponent } from './components/generative-canvas/generative-canvas.component';

interface ChatMessage {
  sender: 'user' | 'agent';
  text: string;
  provider?: string;
  timestamp: Date;
}

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [CommonModule, FormsModule, GenerativeCanvasComponent],
  template: `
    <div class="granbrain-container">
      <!-- HEADER -->
      <header class="app-header">
        <div class="brand-zone">
          <span class="brand-icon">🧠</span>
          <span class="brand-title">GranBrain</span>
          <span class="brand-tag">Obsidian AI OS</span>
        </div>

        <div *ngIf="isAuthenticated" class="header-controls">
          <!-- Provider Selector -->
          <div class="provider-selector">
            <span class="selector-label">IA:</span>
            <button
              *ngFor="let p of providers"
              type="button"
              class="provider-pill"
              [class.active]="selectedProvider === p.id"
              (click)="selectProvider(p.id)"
            >
              {{ p.displayName }}
            </button>
          </div>

          <button type="button" class="btn-logout" (click)="logout()">Sair</button>
        </div>
      </header>

      <!-- MAIN CONTENT -->
      <main class="app-main">
        <!-- LOGIN VIEW -->
        <section *ngIf="!isAuthenticated" class="login-section">
          <div class="login-card">
            <h2>Acesso ao GranBrain</h2>
            <p class="login-desc">Autentique-se para interagir com o cofre Obsidian e o Orquestrador Multi-IA.</p>

            <form (ngSubmit)="login()" class="login-form">
              <div class="form-group">
                <label>Usuário</label>
                <input
                  type="text"
                  name="username"
                  class="input-field"
                  [(ngModel)]="username"
                  required
                  placeholder="Seu usuário"
                />
              </div>

              <div class="form-group">
                <label>Senha</label>
                <input
                  type="password"
                  name="password"
                  class="input-field"
                  [(ngModel)]="password"
                  required
                  placeholder="Sua senha"
                />
              </div>

              <div *ngIf="loginError" class="error-banner">
                {{ loginError }}
              </div>

              <button type="submit" class="btn-primary" [disabled]="loading">
                {{ loading ? 'Entrando...' : 'Entrar no Sistema' }}
              </button>
            </form>
          </div>
        </section>

        <!-- DASHBOARD & CHAT VIEW -->
        <section *ngIf="isAuthenticated" class="workspace-section">
          <!-- Chat Feed -->
          <div class="chat-feed" #chatFeed>
            <div *ngIf="messages.length === 0" class="empty-state">
              <h3>Pronto para orquestrar seu segundo cérebro.</h3>
              <p>Pergunte sobre seus hábitos, kanban, notas ou dê um comando de voz.</p>
            </div>

            <div
              *ngFor="let msg of messages"
              class="chat-bubble"
              [ngClass]="msg.sender === 'user' ? 'bubble-user' : 'bubble-agent'"
            >
              <div class="bubble-header">
                <span class="bubble-sender">{{ msg.sender === 'user' ? 'Você' : (msg.provider || 'GranBrain Agent') }}</span>
                <span class="bubble-time mono-font">{{ msg.timestamp | date:'shortTime' }}</span>
              </div>
              <div class="bubble-body">{{ msg.text }}</div>
            </div>
          </div>

          <!-- GENERATIVE UI CANVAS -->
          <app-generative-canvas
            *ngIf="currentBlocks.length > 0"
            [blocks]="currentBlocks"
            (habitToggled)="onHabitToggled($event)"
            (kanbanTaskMoved)="onKanbanTaskMoved($event)"
            (dynamicFormSubmitted)="onDynamicFormSubmitted($event)"
            (noteSelected)="onNoteSelected($event)"
          ></app-generative-canvas>

          <!-- COMMAND BAR -->
          <div class="command-bar">
            <!-- Voice Button -->
            <button
              type="button"
              class="btn-voice"
              [class.listening]="isListening"
              (click)="toggleVoice()"
              [title]="isListening ? 'Ouvindo... Clique para parar' : 'Comando por voz'"
            >
              🎤
            </button>

            <!-- Text Prompt Input -->
            <input
              type="text"
              class="command-input"
              [(ngModel)]="prompt"
              (keydown.enter)="sendCommand()"
              placeholder="Digite um comando para o cofre ou peça algo..."
              [disabled]="loading"
            />

            <!-- Send Button -->
            <button
              type="button"
              class="btn-send"
              (click)="sendCommand()"
              [disabled]="loading || !prompt.trim()"
            >
              {{ loading ? '...' : 'Enviar' }}
            </button>
          </div>
        </section>
      </main>
    </div>
  `,
  styles: [`
    .granbrain-container {
      display: flex;
      flex-direction: column;
      height: 100vh;
      background: #0d1117;
      color: #f0f6fc;
      overflow: hidden;
    }
    .app-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      padding: 0.75rem 1.5rem;
      background: #161b22;
      border-bottom: 1px solid #30363d;
    }
    .brand-zone { display: flex; align-items: center; gap: 0.5rem; }
    .brand-icon { font-size: 1.4rem; }
    .brand-title { font-weight: 700; font-size: 1.2rem; color: #58a6ff; letter-spacing: 0.5px; }
    .brand-tag { font-size: 0.75rem; color: #8b949e; background: #21262d; padding: 0.15rem 0.4rem; border-radius: 4px; }

    .header-controls { display: flex; align-items: center; gap: 1rem; }
    .provider-selector { display: flex; align-items: center; gap: 0.4rem; }
    .selector-label { font-size: 0.8rem; color: #8b949e; font-weight: 600; }
    .provider-pill {
      background: #21262d;
      border: 1px solid #30363d;
      color: #c9d1d9;
      border-radius: 20px;
      padding: 0.25rem 0.75rem;
      font-size: 0.8rem;
      cursor: pointer;
      transition: all 0.2s;
    }
    .provider-pill.active {
      background: #1f6feb;
      border-color: #58a6ff;
      color: #ffffff;
      font-weight: 600;
    }
    .btn-logout {
      background: transparent;
      border: 1px solid #30363d;
      color: #f85149;
      padding: 0.25rem 0.6rem;
      border-radius: 4px;
      font-size: 0.8rem;
      cursor: pointer;
    }
    .btn-logout:hover { background: rgba(248, 81, 73, 0.1); }

    .app-main {
      flex: 1;
      display: flex;
      flex-direction: column;
      overflow-y: auto;
      padding: 1.5rem;
    }

    /* LOGIN */
    .login-section {
      display: flex;
      justify-content: center;
      align-items: center;
      height: 100%;
    }
    .login-card {
      background: #161b22;
      border: 1px solid #30363d;
      border-radius: 8px;
      padding: 2rem;
      max-width: 400px;
      width: 100%;
      box-shadow: 0 8px 24px rgba(0, 0, 0, 0.4);
    }
    .login-card h2 { margin-top: 0; color: #58a6ff; font-size: 1.4rem; }
    .login-desc { color: #8b949e; font-size: 0.85rem; margin-bottom: 1.5rem; }
    .login-form { display: flex; flex-direction: column; gap: 1rem; }
    .form-group { display: flex; flex-direction: column; gap: 0.4rem; }
    .form-group label { font-size: 0.85rem; color: #c9d1d9; }
    .input-field {
      background: #0d1117;
      border: 1px solid #30363d;
      border-radius: 6px;
      padding: 0.6rem;
      color: #f0f6fc;
      outline: none;
    }
    .input-field:focus { border-color: #58a6ff; }
    .btn-primary {
      background: #238636;
      color: white;
      border: none;
      border-radius: 6px;
      padding: 0.75rem;
      font-weight: 600;
      cursor: pointer;
      margin-top: 0.5rem;
    }
    .btn-primary:hover { background: #2ea043; }
    .btn-primary:disabled { opacity: 0.6; cursor: not-allowed; }
    .error-banner {
      background: rgba(248, 81, 73, 0.15);
      border: 1px solid #f85149;
      color: #ff7b72;
      padding: 0.5rem;
      border-radius: 6px;
      font-size: 0.85rem;
    }

    /* WORKSPACE */
    .workspace-section {
      display: flex;
      flex-direction: column;
      gap: 1rem;
      max-width: 1200px;
      width: 100%;
      margin: 0 auto;
      flex: 1;
    }
    .chat-feed {
      display: flex;
      flex-direction: column;
      gap: 0.75rem;
      flex: 1;
      overflow-y: auto;
    }
    .empty-state {
      text-align: center;
      padding: 3rem 1rem;
      color: #8b949e;
    }
    .empty-state h3 { color: #c9d1d9; margin-bottom: 0.5rem; }
    .chat-bubble {
      max-width: 85%;
      border-radius: 8px;
      padding: 0.75rem 1rem;
      display: flex;
      flex-direction: column;
      gap: 0.25rem;
    }
    .bubble-user {
      align-self: flex-end;
      background: #1f6feb;
      color: #ffffff;
    }
    .bubble-agent {
      align-self: flex-start;
      background: #161b22;
      border: 1px solid #30363d;
      color: #f0f6fc;
    }
    .bubble-header { display: flex; justify-content: space-between; gap: 1rem; font-size: 0.75rem; opacity: 0.8; }
    .bubble-sender { font-weight: 600; }
    .bubble-body { font-size: 0.95rem; line-height: 1.4; white-space: pre-wrap; }

    /* COMMAND BAR */
    .command-bar {
      display: flex;
      gap: 0.5rem;
      align-items: center;
      background: #161b22;
      border: 1px solid #30363d;
      border-radius: 8px;
      padding: 0.5rem;
      margin-top: auto;
    }
    .command-input {
      flex: 1;
      background: transparent;
      border: none;
      color: #f0f6fc;
      padding: 0.5rem;
      font-size: 0.95rem;
      outline: none;
    }
    .btn-voice {
      background: #21262d;
      border: 1px solid #30363d;
      border-radius: 6px;
      padding: 0.5rem 0.75rem;
      font-size: 1.1rem;
      cursor: pointer;
      transition: background 0.2s;
    }
    .btn-voice.listening {
      background: #f85149;
      animation: pulse 1.2s infinite;
    }
    @keyframes pulse {
      0% { transform: scale(1); }
      50% { transform: scale(1.08); }
      100% { transform: scale(1); }
    }
    .btn-send {
      background: #238636;
      color: white;
      border: none;
      border-radius: 6px;
      padding: 0.5rem 1.2rem;
      font-weight: 600;
      cursor: pointer;
    }
    .btn-send:hover { background: #2ea043; }
    .btn-send:disabled { opacity: 0.5; cursor: not-allowed; }
  `]
})
export class AppComponent implements OnInit, OnDestroy {
  public title = 'GranBrain';
  public isAuthenticated = false;
  public username = '';
  public password = '';
  public loginError = '';
  public loading = false;

  public prompt = '';
  public selectedProvider = 'codex';
  public providers: AiProviderStatus[] = [
    { id: 'codex', displayName: 'Codex CLI', planName: 'ChatGPT Plus', executablePath: 'codex', installed: true, description: 'OpenAI Codex CLI' },
    { id: 'claude', displayName: 'Claude Code', planName: 'Claude Pro', executablePath: 'claude', installed: false, description: 'Anthropic Claude Code CLI' },
    { id: 'gemini', displayName: 'Gemini CLI', planName: 'Google AI Pro', executablePath: 'gemini', installed: true, description: 'Google Antigravity / Gemini CLI' }
  ];

  public messages: ChatMessage[] = [];
  public currentBlocks: GenerativeUiBlock[] = [];
  public isListening = false;

  private subs = new Subscription();

  constructor(
    public api: GranBrainApiService,
    public voice: VoiceService
  ) {}

  public ngOnInit(): void {
    this.isAuthenticated = this.api.isAuthenticated;
    if (this.isAuthenticated) {
      this.loadProviders();
      this.loadInitialOverview();
    }

    // Subscribe to voice transcripts
    this.subs.add(
      this.voice.onTranscript$.subscribe((transcript) => {
        if (transcript) {
          this.prompt = transcript;
          this.sendCommand(true);
        }
      })
    );

    this.subs.add(
      this.voice.isListening$.subscribe((listening) => {
        this.isListening = listening;
      })
    );
  }

  public ngOnDestroy(): void {
    this.subs.unsubscribe();
  }

  public login(): void {
    if (!this.username.trim() || !this.password.trim()) return;
    this.loading = true;
    this.loginError = '';

    this.api.login({ username: this.username, password: this.password }).subscribe({
      next: () => {
        this.isAuthenticated = true;
        this.loading = false;
        this.loadProviders();
        this.loadInitialOverview();
      },
      error: (err) => {
        this.loading = false;
        this.loginError = err.error?.message || 'Falha na autenticação.';
      }
    });
  }

  public logout(): void {
    this.api.clearToken();
    this.isAuthenticated = false;
    this.messages = [];
    this.currentBlocks = [];
  }

  public selectProvider(providerId: string): void {
    this.selectedProvider = providerId;
  }

  public toggleVoice(): void {
    if (this.isListening) {
      this.voice.stopListening();
    } else {
      this.voice.startListening();
    }
  }

  public sendCommand(voiceMode = false): void {
    const text = this.prompt.trim();
    if (!text) return;

    this.messages.push({
      sender: 'user',
      text,
      timestamp: new Date()
    });

    this.prompt = '';
    this.loading = true;

    this.api.sendChat(text, this.selectedProvider, voiceMode).subscribe({
      next: (res) => {
        this.loading = false;
        this.messages.push({
          sender: 'agent',
          text: res.replyText,
          provider: res.providerUsed,
          timestamp: new Date(res.timestampUtc)
        });

        if (res.uiBlocks && res.uiBlocks.length > 0) {
          this.currentBlocks = res.uiBlocks;
        }

        if (voiceMode && res.replyText) {
          this.voice.speak(res.replyText);
        }
      },
      error: () => {
        this.loading = false;
        this.messages.push({
          sender: 'agent',
          text: 'Erro ao processar requisição com o orquestrador.',
          timestamp: new Date()
        });
      }
    });
  }

  public onHabitToggled(event: { habit: HabitItem; blockId: string }): void {
    this.api.toggleHabit(event.habit.label).subscribe({
      next: (updatedHabit) => {
        event.habit.completed = updatedHabit.completed;
      }
    });
  }

  public onKanbanTaskMoved(event: {
    boardName: string;
    taskText: string;
    targetColumn: string;
    blockId: string;
  }): void {
    this.api.moveKanbanTask(event.boardName, event.taskText, event.targetColumn).subscribe({
      next: (board) => {
        const blk = this.currentBlocks.find((b) => b.id === event.blockId);
        if (blk) {
          blk.kanban = board;
        }
      }
    });
  }

  public onDynamicFormSubmitted(event: {
    formId: string;
    noteType: string;
    values: Record<string, string>;
    blockId: string;
  }): void {
    const title = event.values['title'] || `Nota-${Date.now()}`;
    const content = event.values['summary'] || event.values['content'] || '';

    this.api.createNote({
      noteType: event.noteType,
      title,
      content,
      extraFields: event.values
    }).subscribe({
      next: (note) => {
        this.messages.push({
          sender: 'agent',
          text: `Nota "${note.title}" criada com sucesso no cofre! (${note.relativePath})`,
          timestamp: new Date()
        });
      }
    });
  }

  public onNoteSelected(event: { note: VaultNoteSummary; blockId: string }): void {
    this.messages.push({
      sender: 'agent',
      text: `Nota selecionada: ${event.note.title} (${event.note.relativePath})`,
      timestamp: new Date()
    });
  }

  private loadProviders(): void {
    this.api.getProviders().subscribe({
      next: (list) => {
        if (list && list.length > 0) {
          this.providers = list;
        }
      }
    });
  }

  private loadInitialOverview(): void {
    this.api.getOverview().subscribe({
      next: (ov) => {
        this.currentBlocks = [
          {
            id: 'ov-kpi',
            type: 'kpi-grid',
            title: 'Resumo Geral do Cofre',
            metrics: [
              { label: 'Hábitos Hoje', value: `${ov.habitsCompletedToday}/${ov.habitsTotalToday}`, accent: 'secondary' },
              { label: 'Notas Zettelkasten', value: `${ov.totalZettelkastenNotes}`, accent: 'primary' },
              { label: 'Inbox Pendente', value: `${ov.inboxPendingCount}`, accent: 'warning' }
            ]
          },
          {
            id: 'ov-habits',
            type: 'habit-checklist',
            title: 'Hábitos do Dia',
            habits: ov.todayHabits
          }
        ];
      }
    });
  }
}
