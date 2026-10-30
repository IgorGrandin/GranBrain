import { Component, EventEmitter, Input, OnInit, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import {
  GenerativeUiBlock,
  HabitItem,
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

  @Output() public habitToggled = new EventEmitter<{ habit: HabitItem; blockId: string }>();
  @Output() public kanbanTaskMoved = new EventEmitter<{ boardName: string; taskText: string; targetColumn: string; blockId: string }>();
  @Output() public dynamicFormSubmitted = new EventEmitter<{ formId: string; noteType: string; values: Record<string, string>; blockId: string }>();
  @Output() public noteSelected = new EventEmitter<{ note: VaultNoteSummary; blockId: string }>();

  public formStore: Record<string, Record<string, string>> = {};
  public noteSearchQuery = '';
  public selectedNote?: VaultNoteSummary;

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
}
