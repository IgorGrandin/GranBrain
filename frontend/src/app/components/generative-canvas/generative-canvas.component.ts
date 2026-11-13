import { Component, EventEmitter, Input, OnInit, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import {
  CalendarEvent,
  CalendarProvider,
  GenerativeUiBlock,
  HabitItem,
  IdeaItem,
  KanbanColumn,
  KanbanTask,
  VaultNoteSummary
} from '../../models/granbrain.models';

@Component({
  selector: 'app-generative-canvas',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule
  ],
  templateUrl: './generative-canvas.component.html',
  styleUrl: './generative-canvas.component.scss'
})
export class GenerativeCanvasComponent implements OnInit {
  @Input() public blocks: GenerativeUiBlock[] = [];
  @Input() public isListening = false;
  @Input() public routineTriagePending = false;
  @Input() public routineRunningJobId: string | null = null;

  @Output() public habitToggled = new EventEmitter<{ habit: HabitItem; blockId: string }>();
  @Output() public kanbanTaskMoved = new EventEmitter<{ boardName: string; taskText: string; targetColumn: string; blockId: string }>();
  @Output() public dynamicFormSubmitted = new EventEmitter<{ formId: string; noteType: string; values: Record<string, string>; blockId: string }>();
  @Output() public noteSelected = new EventEmitter<{ note: VaultNoteSummary; blockId: string }>();
  @Output() public ideaCaptured = new EventEmitter<{ text: string; target?: string; blockId: string }>();
  @Output() public ideaVoiceTriggered = new EventEmitter<{ blockId: string }>();
  @Output() public ideaBrainstormRequested = new EventEmitter<{ ideaId: string; ideaText: string; blockId: string }>();
  @Output() public ideaPromoted = new EventEmitter<{ ideaId: string; target: 'permanente' | 'projeto' | 'descartada'; socraticAnswer?: string; blockId: string }>();
  @Output() public agendaSyncRequested = new EventEmitter<{ blockId: string }>();
  @Output() public calendarNoteRequested = new EventEmitter<{ event: CalendarEvent; blockId: string }>();
  @Output() public routineTriageRequested = new EventEmitter<{ blockId: string }>();
  @Output() public routineJobRunRequested = new EventEmitter<{ jobId: string; blockId: string }>();

  public formStore: Record<string, Record<string, string>> = {};
  public noteSearchQuery = '';
  public selectedNote?: VaultNoteSummary;
  public ideaDraftText = '';
  public socraticAnswer = '';
  public agendaViewMode: 'lista' | 'calendario' = 'lista';
  public agendaProviderFilter: 'Todos' | CalendarProvider = 'Todos';
  public readonly calendarHours = ['08:00', '09:00', '10:00', '11:00', '12:00', '13:00', '14:00', '15:00', '16:00', '17:00', '18:00'];

  constructor() {}

  public ngOnInit(): void {
    this.initFormStores();
  }

  public getFormStore(blockId: string): Record<string, string> {
    if (!this.formStore[blockId]) {
      this.formStore[blockId] = {};
    }
    return this.formStore[blockId];
  }

  private initFormStores(): void {
    for (const block of this.blocks) {
      if (block.type === 'dynamic-form' && block.form) {
        const store = this.getFormStore(block.id);
        for (const field of block.form.fields) {
          if (store[field.key] === undefined) {
            store[field.key] = field.defaultValue || '';
          }
        }
      }
    }
  }

  public getCompletedHabitsCount(habits?: HabitItem[]): number {
    if (!habits) return 0;
    return habits.filter(h => h.completed).length;
  }

  public getHabitsProgressPercent(habits?: HabitItem[]): number {
    if (!habits || habits.length === 0) return 0;
    return (this.getCompletedHabitsCount(habits) / habits.length) * 100;
  }

  public formatWikiLinks(text: string): string {
    if (!text) return '';
    // Format [[WikiLinks]] into styled spans
    return text.replace(/\[\[(.*?)\]\]/g, '<span class="wl">[[$1]]</span>');
  }

  public getFilteredNotes(notes?: VaultNoteSummary[]): VaultNoteSummary[] {
    if (!notes) return [];
    if (!this.noteSearchQuery.trim()) return notes;
    const q = this.noteSearchQuery.toLowerCase();
    return notes.filter(n =>
      n.title.toLowerCase().includes(q) ||
      (n.tags && n.tags.some(t => t.toLowerCase().includes(q))) ||
      (n.excerpt && n.excerpt.toLowerCase().includes(q))
    );
  }

  public onToggleHabit(habit: HabitItem, blockId: string): void {
    this.habitToggled.emit({ habit, blockId });
  }

  public onMoveKanbanTask(
    boardName: string,
    task: KanbanTask,
    currentColumn: string,
    columns: KanbanColumn[],
    blockId: string
  ): void {
    const colIdx = columns.findIndex((c) => c.name === currentColumn);
    const targetIdx = (colIdx + 1) % columns.length;
    const targetColumn = columns[targetIdx]?.name || currentColumn;

    this.kanbanTaskMoved.emit({
      boardName,
      taskText: task.text,
      targetColumn,
      blockId
    });
  }

  public onSubmitForm(block: GenerativeUiBlock): void {
    if (!block.form) return;
    const store = this.getFormStore(block.id);
    this.dynamicFormSubmitted.emit({
      formId: block.form.formId,
      noteType: block.form.noteType,
      values: { ...store },
      blockId: block.id
    });
  }

  public onSelectNote(note: VaultNoteSummary, blockId: string): void {
    this.selectedNote = note;
    this.noteSelected.emit({ note, blockId });
  }

  public onSubmitIdeaCapture(blockId: string): void {
    const text = this.ideaDraftText.trim();
    if (!text) return;
    this.ideaCaptured.emit({ text, target: 'fleeting', blockId });
    this.ideaDraftText = '';
  }

  public onTriggerIdeaVoice(blockId: string): void {
    this.ideaVoiceTriggered.emit({ blockId });
  }

  public onRequestBrainstorm(idea: IdeaItem, blockId: string): void {
    this.ideaBrainstormRequested.emit({ ideaId: idea.id, ideaText: idea.text, blockId });
  }

  public onPromoteIdea(ideaId: string, target: 'permanente' | 'projeto' | 'descartada', blockId: string): void {
    const answer = this.socraticAnswer.trim();
    this.ideaPromoted.emit({
      ideaId,
      target,
      socraticAnswer: answer ? answer : undefined,
      blockId
    });
    if (answer) {
      this.socraticAnswer = '';
    }
  }

  public getIdeaStatusPillClass(status: string): string {
    switch (status) {
      case 'fleeting': return 'pill info';
      case 'brainstorm': return 'pill accent';
      case 'promovida': return 'pill ok';
      default: return 'pill';
    }
  }

  public getIdeaStatusLabel(status: string): string {
    switch (status) {
      case 'fleeting': return 'Fleeting';
      case 'brainstorm': return 'Em brainstorm';
      case 'promovida': return 'Promovida';
      case 'descartada': return 'Descartada';
      default: return status;
    }
  }

  public getConfidencePillClass(confidence: string): string {
    switch (confidence.toLowerCase()) {
      case 'alta': return 'pill ok';
      case 'média':
      case 'media': return 'pill warn';
      default: return 'pill';
    }
  }

  public getSeverityPillClass(severity: string): string {
    switch (severity.toLowerCase()) {
      case 'alto': return 'pill danger';
      case 'médio':
      case 'medio': return 'pill warn';
      default: return 'pill';
    }
  }

  public setAgendaProviderFilter(provider: 'Todos' | CalendarProvider): void {
    this.agendaProviderFilter = provider;
  }

  public getFilteredAgendaEvents(events?: CalendarEvent[]): CalendarEvent[] {
    if (!events) return [];
    if (this.agendaProviderFilter === 'Todos') return events;
    return events.filter((ev) => ev.provider === this.agendaProviderFilter);
  }

  public onAgendaSync(blockId: string): void {
    this.agendaSyncRequested.emit({ blockId });
  }

  public onRequestCalendarNote(event: CalendarEvent, blockId: string): void {
    this.calendarNoteRequested.emit({ event, blockId });
  }

  public shouldRenderNowlineBefore(events: CalendarEvent[], index: number): boolean {
    if (!events || events.length === 0) return false;
    const nowIndex = events.findIndex((e) => e.status === 'now');
    if (nowIndex >= 0) return index === nowIndex;
    const nextIndex = events.findIndex((e) => e.status === 'next');
    if (nextIndex >= 0) return index === nextIndex;
    return false;
  }

  public shouldRenderNowlineAtEnd(events: CalendarEvent[]): boolean {
    if (!events || events.length === 0) return false;
    return !events.some((e) => e.status === 'now' || e.status === 'next');
  }

  public getEventStatusLabel(status: string): string {
    switch (status) {
      case 'now': return 'Em andamento';
      case 'past': return 'Passou';
      case 'next': return 'Próximo';
      default: return status;
    }
  }

  public getCurrentTimeLabel(): string {
    const now = new Date();
    const hh = String(now.getHours()).padStart(2, '0');
    const mm = String(now.getMinutes()).padStart(2, '0');
    return `${hh}:${mm}`;
  }

  public getCalendarNowTopPx(): number {
    const now = new Date();
    const minutesSince8 = (now.getHours() - 8) * 60 + now.getMinutes();
    return Math.max(0, Math.min(600, minutesSince8));
  }

  public getCalEventLeftPct(ev: CalendarEvent): number {
    const total = Math.max(1, ev.calendarTotalColumns || 1);
    const col = Math.max(0, Math.min(total - 1, ev.calendarColumn || 0));
    return (col / total) * 100;
  }

  public getCalEventWidthPct(ev: CalendarEvent): number {
    const total = Math.max(1, ev.calendarTotalColumns || 1);
    return (1 / total) * 100;
  }

  public onRequestRoutineTriage(blockId: string): void {
    this.routineTriageRequested.emit({ blockId });
  }

  public onRunRoutineJob(jobId: string, blockId: string): void {
    this.routineJobRunRequested.emit({ jobId, blockId });
  }

  public trackByBlockId(_: number, block: GenerativeUiBlock): string {
    return block.id;
  }
}
