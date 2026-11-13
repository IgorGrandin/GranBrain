import { Component, OnDestroy, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Subscription } from 'rxjs';
import { GranBrainApiService } from './services/granbrain-api.service';
import { VoiceService } from './services/voice.service';
import {
  AiProviderStatus,
  CalendarEvent,
  CalendarToday,
  GenerativeUiBlock,
  HabitItem,
  IdeasOverview,
  SystemHealthStatus,
  VaultNoteSummary
} from './models/granbrain.models';
import { GenerativeCanvasComponent } from './components/generative-canvas/generative-canvas.component';

export interface ChatMessage {
  sender: 'user' | 'agent';
  text: string;
  provider?: string;
  model?: string;
  timestamp: Date;
}

export interface ToastNotification {
  id: number;
  message: string;
  type: 'ok' | 'warn' | 'danger';
}

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    GenerativeCanvasComponent
  ],
  templateUrl: './app.component.html',
  styleUrl: './app.component.scss'
})
export class AppComponent implements OnInit, OnDestroy {
  public title = 'GranBrain';
  public isAuthenticated = false;
  public username = '';
  public password = '';
  public showPassword = false;
  public loginError = '';
  public loading = false;
  public isDarkMode = false;
  public currentView = 'dashboard';

  public prompt = '';
  public selectedProvider = 'codex';
  public providers: AiProviderStatus[] = [
    { id: 'codex', displayName: 'Codex CLI', planName: 'ChatGPT Plus', model: 'gpt-6-luna-medium', executablePath: 'codex', installed: true, description: 'OpenAI Codex CLI' },
    { id: 'claude', displayName: 'Claude Code', planName: 'Claude Pro', model: 'claude-haiku-5-5-medium', executablePath: 'claude', installed: false, description: 'Anthropic Claude Code CLI' },
    { id: 'gemini', displayName: 'Gemini CLI', planName: 'Google AI Pro', model: 'gemini-3.8-flash-medium', executablePath: 'gemini', installed: true, description: 'Google Antigravity / Gemini CLI' }
  ];

  public messages: ChatMessage[] = [];
  public currentBlocks: GenerativeUiBlock[] = [];
  public isListening = false;
  public toasts: ToastNotification[] = [];
  public systemHealth: SystemHealthStatus | null = null;
  public isBackendOffline = false;
  public healthChecking = false;

  private toastIdCounter = 0;
  private subs = new Subscription();
  private pendingIdeaVoiceBlockId: string | null = null;

  constructor(
    public api: GranBrainApiService,
    public voice: VoiceService
  ) {}

  public get activeProvider(): AiProviderStatus | undefined {
    return this.providers.find(p => p.id === this.selectedProvider);
  }

  public get activeModel(): string {
    return this.activeProvider?.model || 'modelo básico';
  }

  public get inputPlaceholder(): string {
    return `[${this.activeModel}] Digite comando ou 'cli: pergunta'...`;
  }

  public get vaultBadgeClass(): string {
    if (this.isBackendOffline || (this.systemHealth && !this.systemHealth.vaultAccessible)) {
      return 'danger';
    }
    if (this.systemHealth?.status === 'degraded') {
      return 'warn';
    }
    return 'ok';
  }

  public get vaultBadgeLabel(): string {
    if (this.isBackendOffline || (this.systemHealth && !this.systemHealth.vaultAccessible)) {
      return 'Offline';
    }
    if (this.systemHealth?.status === 'degraded') {
      return 'Degradado';
    }
    return 'Ativo';
  }

  public get tunnelBadgeClass(): string {
    if (this.isBackendOffline) {
      return 'danger';
    }
    return this.systemHealth?.tunnelStatus === 'ok' ? 'ok' : 'warn';
  }

  public get tunnelBadgeLabel(): string {
    if (this.isBackendOffline) {
      return 'Sem Rede';
    }
    return this.systemHealth?.tunnelStatus === 'ok' ? 'HTTPS · Tunnel' : 'Local';
  }

  public get syncBadgeClass(): string {
    if (this.isBackendOffline || this.systemHealth?.syncStatus === 'error') {
      return 'danger';
    }
    return this.systemHealth?.syncStatus === 'ok' ? 'ok' : 'warn';
  }

  public get syncBadgeLabel(): string {
    if (this.isBackendOffline || this.systemHealth?.syncStatus === 'error') {
      return 'Erro Sync';
    }
    return this.systemHealth?.syncStatus === 'ok' ? 'Drive Sync' : 'Sync Pendente';
  }

  public ngOnInit(): void {
    this.isAuthenticated = this.api.isAuthenticated;
    if (this.isAuthenticated) {
      this.loadProviders();
      this.loadInitialOverview();
      this.refreshHealth();
    }

    // Subscribe to voice transcripts
    this.subs.add(
      this.voice.onTranscript$.subscribe((transcript) => {
        if (transcript) {
          if (this.pendingIdeaVoiceBlockId) {
            const blockId = this.pendingIdeaVoiceBlockId;
            this.pendingIdeaVoiceBlockId = null;
            this.onIdeaCaptured({ text: transcript, target: 'fleeting', blockId });
            return;
          }
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

  public showToast(message: string, type: 'ok' | 'warn' | 'danger' = 'ok'): void {
    const toast: ToastNotification = {
      id: ++this.toastIdCounter,
      message,
      type
    };
    this.toasts.push(toast);
    setTimeout(() => {
      this.toasts = this.toasts.filter(t => t.id !== toast.id);
    }, 4000);
  }

  public toggleTheme(): void {
    this.isDarkMode = !this.isDarkMode;
    if (this.isDarkMode) {
      document.documentElement.setAttribute('data-theme', 'dark');
    } else {
      document.documentElement.removeAttribute('data-theme');
    }
  }

  public login(): void {
    if (!this.username.trim() || !this.password.trim()) return;
    this.loading = true;
    this.loginError = '';

    this.api.login({ username: this.username, password: this.password }).subscribe({
      next: () => {
        this.isAuthenticated = true;
        this.loading = false;
        this.showToast('Conectado com sucesso ao cofre!', 'ok');
        this.loadProviders();
        this.loadInitialOverview();
        this.refreshHealth();
      },
      error: (err) => {
        this.loading = false;
        this.loginError = err.error?.message || 'Falha na autenticação. Verifique usuário e senha.';
        this.showToast('Credenciais inválidas', 'warn');
      }
    });
  }

  public logout(): void {
    this.api.clearToken();
    this.isAuthenticated = false;
    this.messages = [];
    this.currentBlocks = [];
    this.systemHealth = null;
    this.isBackendOffline = false;
    this.showToast('Sessão encerrada', 'ok');
  }

  public refreshHealth(): void {
    this.healthChecking = true;
    this.api.getHealth().subscribe({
      next: (health) => {
        this.healthChecking = false;
        this.systemHealth = health;
        if (this.isBackendOffline) {
          this.isBackendOffline = false;
          this.showToast('Conexão segura restabelecida com o cofre.', 'ok');
        }
      },
      error: () => {
        this.healthChecking = false;
        if (!this.isBackendOffline) {
          this.isBackendOffline = true;
          this.showToast('Conexão com a API perdida. Tentando reconectar...', 'warn');
        }
      }
    });
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
    const currentModel = this.activeModel;

    this.api.sendChat(text, this.selectedProvider, voiceMode).subscribe({
      next: (res) => {
        this.loading = false;
        this.messages.push({
          sender: 'agent',
          text: res.replyText,
          provider: res.providerUsed,
          model: currentModel,
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
        this.showToast('Erro ao comunicar com o orquestrador', 'danger');
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
        this.showToast(`Hábito "${event.habit.label}" atualizado`, 'ok');
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
        this.showToast(`Tarefa movida para ${event.targetColumn}`, 'ok');
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
          model: this.activeModel,
          timestamp: new Date()
        });
        this.showToast(`Nota "${note.title}" criada`, 'ok');
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

  public openDashboardView(): void {
    this.currentView = 'dashboard';
    this.loadInitialOverview();
  }

  public openIdeasView(): void {
    this.currentView = 'ideas';
    this.loadIdeasOverview();
  }

  public openAgendaView(): void {
    this.currentView = 'agenda';
    this.loadAgendaToday();
  }

  public onIdeaCaptured(event: { text: string; target?: string; blockId: string }): void {
    this.api.captureFleetingIdea({ text: event.text, target: event.target || 'fleeting' }).subscribe({
      next: () => {
        this.showToast('Ideia capturada em Fleeting.md', 'ok');
        this.loadIdeasOverview();
      },
      error: () => this.showToast('Falha ao capturar ideia', 'danger')
    });
  }

  public onIdeaVoiceTriggered(event: { blockId: string }): void {
    this.pendingIdeaVoiceBlockId = this.isListening ? null : event.blockId;
    this.toggleVoice();
  }

  public onIdeaBrainstormRequested(event: { ideaId: string; ideaText: string; blockId: string }): void {
    this.api.startBrainstorm({ ideaId: event.ideaId, ideaText: event.ideaText }).subscribe({
      next: () => {
        this.showToast('Sessão de brainstorming iniciada', 'ok');
        this.loadIdeasOverview();
      },
      error: () => this.showToast('Falha ao iniciar brainstorming', 'danger')
    });
  }

  public onIdeaPromoted(event: { ideaId: string; target: 'permanente' | 'projeto' | 'descartada'; socraticAnswer?: string; blockId: string }): void {
    this.api.promoteIdea({
      ideaId: event.ideaId,
      target: event.target,
      socraticAnswer: event.socraticAnswer
    }).subscribe({
      next: (res) => {
        const label = res.status === 'descartada' ? 'Ideia descartada' : `Ideia promovida (${event.target})`;
        this.showToast(label, 'ok');
        this.loadIdeasOverview();
      },
      error: () => this.showToast('Falha ao atualizar ideia', 'danger')
    });
  }

  private loadIdeasOverview(): void {
    this.api.getIdeasOverview().subscribe({
      next: (ov) => this.applyIdeasBlocks(ov),
      error: () => this.showToast('Erro ao carregar esteira de ideias', 'danger')
    });
  }

  private applyIdeasBlocks(ov: IdeasOverview): void {
    this.currentBlocks = [
      {
        id: 'ideas-capture',
        type: 'idea-capture',
        title: 'Captura Rápida de Ideias',
        subtitle: 'Gravada com horário em zettelkasten/Fleeting.md',
        ideasOverview: ov
      },
      {
        id: 'ideas-brainstorm',
        type: 'brainstorm-canvas',
        title: 'Esteira de Ideação & Brainstorming Socrático',
        subtitle: 'Fleeting -> Brainstorm -> Permanente / Projetos',
        ideasOverview: ov
      }
    ];
  }

  public onAgendaSyncRequested(event: { blockId: string }): void {
    const agendaDate = this.getActiveAgendaDate(event.blockId);
    this.api.syncCalendar(agendaDate).subscribe({
      next: (res) => {
        this.applyAgendaBlock(res.agenda);
        this.showToast(`Agenda sincronizada (${res.syncedCount} eventos)`, 'ok');
      },
      error: () => this.showToast('Falha ao sincronizar agenda', 'danger')
    });
  }

  public onCalendarNoteRequested(event: { event: CalendarEvent; blockId: string }): void {
    const agendaDate = this.getActiveAgendaDate(event.blockId);
    this.api.createCalendarEventNote(event.event.id, undefined, agendaDate).subscribe({
      next: (res) => {
        event.event.notePath = res.notePath;
        this.showToast(
          res.created ? `Nota de reunião criada em ${res.notePath}` : `Nota aberta: ${res.notePath}`,
          'ok'
        );
      },
      error: () => this.showToast('Falha ao gerar nota da reunião', 'danger')
    });
  }

  private getActiveAgendaDate(blockId?: string): string | undefined {
    const blk = this.currentBlocks.find((b) =>
      b.type === 'agenda-timeline' && (!blockId || b.id === blockId)
    );
    return blk?.calendarAgenda?.date;
  }

  private loadAgendaToday(): void {
    this.api.getCalendarToday().subscribe({
      next: (agenda) => this.applyAgendaBlock(agenda),
      error: () => this.showToast('Erro ao carregar agenda de hoje', 'danger')
    });
  }

  private applyAgendaBlock(agenda: CalendarToday): void {
    this.currentBlocks = [
      {
        id: 'agenda-today',
        type: 'agenda-timeline',
        title: 'Agenda & Sincronia do Dia',
        subtitle: `${agenda.date} · ${agenda.timeZone}`,
        calendarAgenda: agenda
      }
    ];
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
