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
  imports: [CommonModule, FormsModule],
  template: `
    <div class="generative-canvas">
      <div *ngFor="let block of blocks" class="canvas-block" [ngClass]="'block-' + block.type">
        
        <!-- 1. KPI GRID BLOCK -->
        <section
          *ngIf="block.type === 'kpi-grid' && block.metrics"
          class="ui-card kpi-grid-card"
          data-block-type="kpi-grid"
        >
          <div class="card-header">
            <h3 class="card-title">{{ block.title }}</h3>
            <span *ngIf="block.subtitle" class="card-subtitle">{{ block.subtitle }}</span>
          </div>
          <div class="kpi-grid">
            <div
              *ngFor="let metric of block.metrics"
              class="kpi-item"
              [ngClass]="metric.accent ? 'accent-' + metric.accent : 'accent-primary'"
            >
              <div class="kpi-label">{{ metric.label }}</div>
              <div class="kpi-value mono-font">{{ metric.value }}</div>
              <div *ngIf="metric.subtitle" class="kpi-sub">{{ metric.subtitle }}</div>
            </div>
          </div>
        </section>

        <!-- 2. HABIT CHECKLIST BLOCK -->
        <section
          *ngIf="block.type === 'habit-checklist' && block.habits"
          class="ui-card habits-card"
          data-block-type="habit-checklist"
        >
          <div class="card-header">
            <h3 class="card-title">{{ block.title }}</h3>
            <span *ngIf="block.subtitle" class="card-subtitle">{{ block.subtitle }}</span>
          </div>
          <div class="habits-list">
            <div
              *ngFor="let habit of block.habits"
              class="habit-row"
              [class.completed]="habit.completed"
            >
              <button
                type="button"
                class="habit-toggle-btn"
                data-habit-toggle
                (click)="onToggleHabit(habit, block.id)"
              >
                <span class="checkbox-indicator">{{ habit.completed ? '☑' : '☐' }}</span>
                <span class="habit-time mono-font">{{ habit.time }}</span>
                <span class="habit-label">{{ habit.label }}</span>
              </button>
            </div>
          </div>
        </section>

        <!-- 3. KANBAN BOARD BLOCK -->
        <section
          *ngIf="block.type === 'kanban-board' && block.kanban"
          class="ui-card kanban-card"
          data-block-type="kanban-board"
        >
          <div class="card-header">
            <h3 class="card-title">{{ block.title }} ({{ block.kanban.boardName }})</h3>
            <span *ngIf="block.subtitle" class="card-subtitle">{{ block.subtitle }}</span>
          </div>
          <div class="kanban-columns">
            <div *ngFor="let col of block.kanban.columns" class="kanban-column">
              <div class="column-header">
                <h4>{{ col.name }}</h4>
                <span class="count-badge">{{ col.tasks.length }}</span>
              </div>
              <div class="task-list">
                <div *ngFor="let task of col.tasks" class="task-card">
                  <div class="task-text">{{ task.text }}</div>
                  <button
                    type="button"
                    class="move-btn"
                    data-kanban-move
                    (click)="onMoveKanbanTask(block.kanban.boardName, task, col.name, block.kanban.columns, block.id)"
                  >
                    Mover →
                  </button>
                </div>
              </div>
            </div>
          </div>
        </section>

        <!-- 4. DYNAMIC FORM BLOCK -->
        <section
          *ngIf="block.type === 'dynamic-form' && block.form"
          class="ui-card dynamic-form-card"
          data-block-type="dynamic-form"
        >
          <div class="card-header">
            <h3 class="card-title">{{ block.title }}</h3>
            <span *ngIf="block.subtitle" class="card-subtitle">{{ block.subtitle }}</span>
          </div>
          <form (ngSubmit)="onSubmitForm(block)" class="form-container">
            <div *ngFor="let field of block.form.fields" class="form-group">
              <label class="form-label">
                {{ field.label }}
                <span *ngIf="field.required" class="required-star">*</span>
              </label>

              <!-- Text Field -->
              <input
                *ngIf="field.fieldType === 'text' || field.fieldType === 'date'"
                [type]="field.fieldType"
                class="form-control"
                [placeholder]="field.placeholder || ''"
                [(ngModel)]="getFormStore(block.id)[field.key]"
                [name]="block.id + '_' + field.key"
                [required]="!!field.required"
              />

              <!-- Textarea Field -->
              <textarea
                *ngIf="field.fieldType === 'textarea'"
                rows="3"
                class="form-control"
                [placeholder]="field.placeholder || ''"
                [(ngModel)]="getFormStore(block.id)[field.key]"
                [name]="block.id + '_' + field.key"
                [required]="!!field.required"
              ></textarea>

              <!-- Select Field -->
              <select
                *ngIf="field.fieldType === 'select'"
                class="form-control"
                [(ngModel)]="getFormStore(block.id)[field.key]"
                [name]="block.id + '_' + field.key"
                [required]="!!field.required"
              >
                <option *ngFor="let opt of field.options" [value]="opt">{{ opt }}</option>
              </select>
            </div>

            <button type="submit" class="submit-btn" data-form-submit>
              {{ block.form.submitLabel || 'Salvar' }}
            </button>
          </form>
        </section>

        <!-- 5. MARKDOWN NOTE LIST BLOCK -->
        <section
          *ngIf="block.type === 'markdown-note-list' && block.notes"
          class="ui-card note-list-card"
          data-block-type="markdown-note-list"
        >
          <div class="card-header">
            <h3 class="card-title">{{ block.title }}</h3>
            <span *ngIf="block.subtitle" class="card-subtitle">{{ block.subtitle }}</span>
          </div>
          <div class="notes-grid">
            <div
              *ngFor="let note of block.notes"
              class="note-card"
              (click)="onSelectNote(note, block.id)"
            >
              <div class="note-top">
                <span class="note-title">{{ note.title }}</span>
                <span class="note-category mono-font">{{ note.category }}</span>
              </div>
              <p *ngIf="note.excerpt" class="note-excerpt">{{ note.excerpt }}</p>
              <div *ngIf="note.tags && note.tags.length" class="tags-container">
                <span *ngFor="let tag of note.tags" class="tag-pill">#{{ tag }}</span>
              </div>
            </div>
          </div>
        </section>

      </div>
    </div>
  `,
  styles: [`
    .generative-canvas {
      display: flex;
      flex-direction: column;
      gap: 1.5rem;
      width: 100%;
      margin: 1rem 0;
    }
    .ui-card {
      background: #161b22;
      border: 1px solid #30363d;
      border-radius: 8px;
      padding: 1.25rem;
      box-shadow: 0 4px 12px rgba(0, 0, 0, 0.25);
    }
    .card-header {
      margin-bottom: 1rem;
      border-bottom: 1px solid #21262d;
      padding-bottom: 0.5rem;
    }
    .card-title {
      font-size: 1.15rem;
      font-weight: 600;
      color: #58a6ff;
      margin: 0;
    }
    .card-subtitle {
      font-size: 0.85rem;
      color: #8b949e;
    }

    /* KPI Grid */
    .kpi-grid {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(180px, 1fr));
      gap: 1rem;
    }
    .kpi-item {
      background: #0d1117;
      border: 1px solid #21262d;
      border-radius: 6px;
      padding: 1rem;
      border-left: 4px solid #58a6ff;
    }
    .kpi-item.accent-secondary { border-left-color: #7ee787; }
    .kpi-item.accent-warning { border-left-color: #f2cc60; }
    .kpi-item.accent-danger { border-left-color: #f85149; }
    .kpi-label { font-size: 0.8rem; color: #8b949e; text-transform: uppercase; }
    .kpi-value { font-size: 1.6rem; font-weight: bold; color: #f0f6fc; margin: 0.25rem 0; }
    .kpi-sub { font-size: 0.75rem; color: #8b949e; }

    /* Habits */
    .habits-list { display: flex; flex-direction: column; gap: 0.5rem; }
    .habit-row {
      background: #0d1117;
      border: 1px solid #21262d;
      border-radius: 6px;
      transition: background 0.2s;
    }
    .habit-row.completed { opacity: 0.75; border-color: #238636; }
    .habit-toggle-btn {
      width: 100%;
      background: transparent;
      border: none;
      color: #f0f6fc;
      padding: 0.75rem 1rem;
      text-align: left;
      cursor: pointer;
      display: flex;
      align-items: center;
      gap: 0.75rem;
      font-size: 0.95rem;
    }
    .checkbox-indicator { font-size: 1.2rem; color: #58a6ff; }
    .habit-row.completed .checkbox-indicator { color: #7ee787; }
    .habit-time { color: #8b949e; font-size: 0.85rem; }

    /* Kanban */
    .kanban-columns {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(220px, 1fr));
      gap: 1rem;
      overflow-x: auto;
    }
    .kanban-column {
      background: #0d1117;
      border: 1px solid #21262d;
      border-radius: 6px;
      padding: 0.75rem;
      display: flex;
      flex-direction: column;
      gap: 0.75rem;
    }
    .column-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      border-bottom: 1px solid #21262d;
      padding-bottom: 0.4rem;
    }
    .column-header h4 { margin: 0; font-size: 0.95rem; color: #f0f6fc; }
    .count-badge {
      background: #21262d;
      padding: 0.1rem 0.5rem;
      border-radius: 12px;
      font-size: 0.75rem;
      color: #8b949e;
    }
    .task-list { display: flex; flex-direction: column; gap: 0.5rem; }
    .task-card {
      background: #161b22;
      border: 1px solid #30363d;
      border-radius: 4px;
      padding: 0.6rem;
      display: flex;
      justify-content: space-between;
      align-items: center;
      gap: 0.5rem;
    }
    .task-text { font-size: 0.85rem; color: #f0f6fc; }
    .move-btn {
      background: #21262d;
      color: #58a6ff;
      border: 1px solid #30363d;
      border-radius: 4px;
      padding: 0.2rem 0.5rem;
      font-size: 0.75rem;
      cursor: pointer;
    }
    .move-btn:hover { background: #30363d; }

    /* Dynamic Form */
    .form-container { display: flex; flex-direction: column; gap: 1rem; }
    .form-group { display: flex; flex-direction: column; gap: 0.3rem; }
    .form-label { font-size: 0.85rem; color: #c9d1d9; font-weight: 500; }
    .required-star { color: #f85149; }
    .form-control {
      background: #0d1117;
      border: 1px solid #30363d;
      border-radius: 6px;
      padding: 0.6rem;
      color: #f0f6fc;
      font-size: 0.9rem;
      outline: none;
    }
    .form-control:focus { border-color: #58a6ff; }
    .submit-btn {
      align-self: flex-start;
      background: #238636;
      color: #ffffff;
      border: none;
      border-radius: 6px;
      padding: 0.6rem 1.2rem;
      font-weight: 600;
      cursor: pointer;
    }
    .submit-btn:hover { background: #2ea043; }

    /* Notes */
    .notes-grid { display: flex; flex-direction: column; gap: 0.75rem; }
    .note-card {
      background: #0d1117;
      border: 1px solid #21262d;
      border-radius: 6px;
      padding: 0.85rem;
      cursor: pointer;
      transition: border-color 0.2s;
    }
    .note-card:hover { border-color: #58a6ff; }
    .note-top { display: flex; justify-content: space-between; align-items: center; margin-bottom: 0.4rem; }
    .note-title { font-weight: 600; font-size: 0.95rem; color: #58a6ff; }
    .note-category { font-size: 0.75rem; color: #8b949e; background: #161b22; padding: 0.15rem 0.4rem; border-radius: 4px; }
    .note-excerpt { font-size: 0.85rem; color: #8b949e; margin: 0 0 0.5rem 0; }
    .tags-container { display: flex; gap: 0.4rem; flex-wrap: wrap; }
    .tag-pill { font-size: 0.75rem; color: #7ee787; background: #1f242c; padding: 0.1rem 0.4rem; border-radius: 4px; }
  `]
})
export class GenerativeCanvasComponent implements OnInit {
  @Input() public blocks: GenerativeUiBlock[] = [];

  @Output() public habitToggled = new EventEmitter<{ habit: HabitItem; blockId: string }>();
  @Output() public kanbanTaskMoved = new EventEmitter<{ boardName: string; taskText: string; targetColumn: string; blockId: string }>();
  @Output() public dynamicFormSubmitted = new EventEmitter<{ formId: string; noteType: string; values: Record<string, string>; blockId: string }>();
  @Output() public noteSelected = new EventEmitter<{ note: VaultNoteSummary; blockId: string }>();

  public formStore: Record<string, Record<string, string>> = {};

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
    this.noteSelected.emit({ note, blockId });
  }
}
