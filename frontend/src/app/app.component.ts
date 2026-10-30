import { Component, OnDestroy, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Subscription } from 'rxjs';
import {
  IonApp,
  IonHeader,
  IonToolbar,
  IonTitle,
  IonButtons,
  IonButton,
  IonIcon,
  IonBadge,
  IonChip,
  IonContent,
  IonFooter,
  IonCard,
  IonCardHeader,
  IonCardTitle,
  IonCardSubtitle,
  IonCardContent,
  IonItem,
  IonLabel,
  IonInput,
  IonSpinner
} from '@ionic/angular/standalone';
import { addIcons } from 'ionicons';
import {
  micOutline,
  micOffOutline,
  sendOutline,
  logOutOutline,
  hardwareChipOutline,
  sparklesOutline
} from 'ionicons/icons';
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
  imports: [
    CommonModule,
    FormsModule,
    IonApp,
    IonHeader,
    IonToolbar,
    IonTitle,
    IonButtons,
    IonButton,
    IonIcon,
    IonBadge,
    IonChip,
    IonContent,
    IonFooter,
    IonCard,
    IonCardHeader,
    IonCardTitle,
    IonCardSubtitle,
    IonCardContent,
    IonItem,
    IonLabel,
    IonInput,
    IonSpinner,
    GenerativeCanvasComponent
  ],
  template: `
    <ion-app class="granbrain-app">
      <!-- HEADER -->
      <ion-header class="ion-no-border">
        <ion-toolbar color="dark">
          <ion-title>
            <div class="brand-zone">
              <span class="brand-icon">🧠</span>
              <span class="brand-title">GranBrain</span>
              <ion-badge color="dark" class="brand-badge">Obsidian AI OS</ion-badge>
            </div>
          </ion-title>

          <ion-buttons slot="end" *ngIf="isAuthenticated">
            <!-- Provider Selector Chips -->
            <div class="provider-selector">
              <ion-chip
                *ngFor="let p of providers"
                [color]="selectedProvider === p.id ? 'primary' : 'medium'"
                (click)="selectProvider(p.id)"
                class="provider-chip"
              >
                <ion-icon name="hardware-chip-outline"></ion-icon>
                <ion-label>{{ p.displayName }}</ion-label>
              </ion-chip>
            </div>

            <ion-button color="danger" fill="clear" (click)="logout()" title="Sair">
              <ion-icon slot="icon-only" name="log-out-outline"></ion-icon>
            </ion-button>
          </ion-buttons>
        </ion-toolbar>
      </ion-header>

      <!-- CONTENT -->
      <ion-content class="ion-padding" [fullscreen]="true">
        
        <!-- LOGIN VIEW -->
        <div *ngIf="!isAuthenticated" class="login-wrapper">
          <ion-card class="login-card">
            <ion-card-header>
              <ion-card-title color="primary">Acesso ao GranBrain</ion-card-title>
              <ion-card-subtitle>Autentique-se para interagir com o cofre Obsidian e o Orquestrador Multi-IA.</ion-card-subtitle>
            </ion-card-header>

            <ion-card-content>
              <form (ngSubmit)="login()" class="login-form">
                <ion-item fill="outline" class="form-item ion-margin-bottom">
                  <ion-label position="stacked">Usuário</ion-label>
                  <ion-input
                    type="text"
                    name="username"
                    [(ngModel)]="username"
                    required
                    placeholder="Seu usuário"
                  ></ion-input>
                </ion-item>

                <ion-item fill="outline" class="form-item ion-margin-bottom">
                  <ion-label position="stacked">Senha</ion-label>
                  <ion-input
                    type="password"
                    name="password"
                    [(ngModel)]="password"
                    required
                    placeholder="Sua senha"
                  ></ion-input>
                </ion-item>

                <div *ngIf="loginError" class="error-banner">
                  {{ loginError }}
                </div>

                <ion-button expand="block" color="success" type="submit" [disabled]="loading" class="ion-margin-top">
                  <ion-spinner *ngIf="loading" name="crescent"></ion-spinner>
                  <span *ngIf="!loading">Entrar no Sistema</span>
                </ion-button>
              </form>
            </ion-card-content>
          </ion-card>
        </div>

        <!-- WORKSPACE VIEW -->
        <div *ngIf="isAuthenticated" class="workspace-wrapper">
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
        </div>

      </ion-content>

      <!-- COMMAND BAR FOOTER -->
      <ion-footer *ngIf="isAuthenticated" class="ion-no-border">
        <ion-toolbar color="dark" class="command-toolbar">
          <div class="command-bar">
            <!-- Voice Button -->
            <ion-button
              shape="round"
              [color]="isListening ? 'danger' : 'medium'"
              fill="solid"
              (click)="toggleVoice()"
              [title]="isListening ? 'Ouvindo... Clique para parar' : 'Comando por voz'"
              class="btn-voice"
            >
              <ion-icon slot="icon-only" [name]="isListening ? 'mic-off-outline' : 'mic-outline'"></ion-icon>
            </ion-button>

            <!-- Text Prompt Input -->
            <ion-input
              class="command-input"
              [(ngModel)]="prompt"
              (keydown.enter)="sendCommand()"
              placeholder="Digite um comando para o cofre ou 'cli: pergunta para a IA'..."
              [disabled]="loading"
            ></ion-input>

            <!-- Send Button -->
            <ion-button
              shape="round"
              color="success"
              (click)="sendCommand()"
              [disabled]="loading || !prompt.trim()"
              class="btn-send"
            >
              <ion-spinner *ngIf="loading" name="dots"></ion-spinner>
              <ion-icon *ngIf="!loading" slot="icon-only" name="send-outline"></ion-icon>
            </ion-button>
          </div>
        </ion-toolbar>
      </ion-footer>
    </ion-app>
  `,
  styles: [`
    .granbrain-app {
      background: #0d1117;
      color: #f0f6fc;
    }
    .brand-zone {
      display: flex;
      align-items: center;
      gap: 0.5rem;
    }
    .brand-icon { font-size: 1.3rem; }
    .brand-title {
      font-weight: 700;
      font-size: 1.15rem;
      color: #58a6ff;
      letter-spacing: 0.5px;
    }
    .brand-badge {
      font-size: 0.7rem;
      border: 1px solid #30363d;
    }
    .provider-selector {
      display: flex;
      gap: 0.3rem;
      align-items: center;
      margin-right: 0.5rem;
    }
    .provider-chip {
      cursor: pointer;
      font-size: 0.75rem;
    }

    /* LOGIN */
    .login-wrapper {
      display: flex;
      justify-content: center;
      align-items: center;
      min-height: 80vh;
    }
    .login-card {
      --background: #161b22;
      border: 1px solid #30363d;
      border-radius: 8px;
      max-width: 420px;
      width: 100%;
      box-shadow: 0 8px 24px rgba(0, 0, 0, 0.4);
    }
    .form-item {
      --background: #0d1117;
      --border-color: #30363d;
      border-radius: 6px;
    }
    .error-banner {
      background: rgba(248, 81, 73, 0.15);
      border: 1px solid #f85149;
      color: #ff7b72;
      padding: 0.6rem;
      border-radius: 6px;
      font-size: 0.85rem;
      margin-top: 0.5rem;
    }

    /* WORKSPACE */
    .workspace-wrapper {
      display: flex;
      flex-direction: column;
      gap: 1rem;
      max-width: 1200px;
      margin: 0 auto;
      padding-bottom: 1rem;
    }
    .chat-feed {
      display: flex;
      flex-direction: column;
      gap: 0.75rem;
      min-height: 120px;
      max-height: 400px;
      overflow-y: auto;
    }
    .empty-state {
      text-align: center;
      padding: 2rem 1rem;
      color: #8b949e;
    }
    .empty-state h3 { color: #c9d1d9; margin-bottom: 0.3rem; }
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
    .bubble-header {
      display: flex;
      justify-content: space-between;
      gap: 1rem;
      font-size: 0.75rem;
      opacity: 0.8;
    }
    .bubble-sender { font-weight: 600; }
    .bubble-body { font-size: 0.95rem; line-height: 1.4; white-space: pre-wrap; }

    /* COMMAND BAR */
    .command-toolbar {
      --background: #161b22;
      border-top: 1px solid #30363d;
      padding: 0.3rem 0.5rem;
    }
    .command-bar {
      display: flex;
      gap: 0.5rem;
      align-items: center;
      max-width: 1200px;
      margin: 0 auto;
      width: 100%;
    }
    .command-input {
      --background: #0d1117;
      --color: #f0f6fc;
      --placeholder-color: #8b949e;
      --border-color: #30363d;
      border: 1px solid #30363d;
      border-radius: 20px;
      padding: 0 0.8rem;
    }
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
  ) {
    addIcons({
      micOutline,
      micOffOutline,
      sendOutline,
      logOutOutline,
      hardwareChipOutline,
      sparklesOutline
    });
  }

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
      next: (habits) => {
        const list = Array.isArray(habits) ? habits : [habits];
        const match = list.find(
          (h) => h.label === event.habit.label || (h.fullText && h.fullText.includes(event.habit.label))
        );
        if (match) {
          event.habit.completed = match.completed;
        } else {
          event.habit.completed = !event.habit.completed;
        }
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
