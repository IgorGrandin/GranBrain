import { Component, EventEmitter, Input, OnInit, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import {
  IonCard,
  IonCardHeader,
  IonCardTitle,
  IonCardSubtitle,
  IonCardContent,
  IonBadge,
  IonButton,
  IonIcon,
  IonItem,
  IonList,
  IonLabel,
  IonCheckbox,
  IonInput,
  IonTextarea,
  IonSelect,
  IonSelectOption,
  IonGrid,
  IonRow,
  IonCol,
  IonChip
} from '@ionic/angular/standalone';
import { addIcons } from 'ionicons';
import {
  checkmarkCircle,
  arrowForwardOutline,
  saveOutline,
  documentTextOutline,
  listOutline,
  gridOutline,
  calendarOutline,
  sparklesOutline,
  timeOutline
} from 'ionicons/icons';
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
    FormsModule,
    IonCard,
    IonCardHeader,
    IonCardTitle,
    IonCardSubtitle,
    IonCardContent,
    IonBadge,
    IonButton,
    IonIcon,
    IonItem,
    IonList,
    IonLabel,
    IonCheckbox,
    IonInput,
    IonTextarea,
    IonSelect,
    IonSelectOption,
    IonGrid,
    IonRow,
    IonCol,
    IonChip
  ],
  template: `
    <div class="generative-canvas">
      <div *ngFor="let block of blocks" class="canvas-block" [ngClass]="'block-' + block.type">
        
        <!-- 1. KPI GRID BLOCK -->
        <ion-card
          *ngIf="block.type === 'kpi-grid' && block.metrics"
          class="ui-card kpi-grid-card"
          data-block-type="kpi-grid"
        >
          <ion-card-header>
            <div class="card-header-row">
              <ion-card-title>{{ block.title }}</ion-card-title>
              <ion-badge color="primary">KPIs</ion-badge>
            </div>
            <ion-card-subtitle *ngIf="block.subtitle">{{ block.subtitle }}</ion-card-subtitle>
          </ion-card-header>
          <ion-card-content>
            <ion-grid class="ion-no-padding">
              <ion-row>
                <ion-col size="12" sizeSm="6" sizeMd="3" *ngFor="let metric of block.metrics">
                  <div class="kpi-box" [ngClass]="metric.accent ? 'accent-' + metric.accent : 'accent-primary'">
                    <div class="kpi-label">{{ metric.label }}</div>
                    <div class="kpi-value mono-font">{{ metric.value }}</div>
                    <div *ngIf="metric.subtitle" class="kpi-sub">{{ metric.subtitle }}</div>
                  </div>
                </ion-col>
              </ion-row>
            </ion-grid>
          </ion-card-content>
        </ion-card>

        <!-- 2. HABIT CHECKLIST BLOCK -->
        <ion-card
          *ngIf="block.type === 'habit-checklist' && block.habits"
          class="ui-card habits-card"
          data-block-type="habit-checklist"
        >
          <ion-card-header>
            <div class="card-header-row">
              <ion-card-title>{{ block.title }}</ion-card-title>
              <ion-badge color="success">Rotina</ion-badge>
            </div>
            <ion-card-subtitle *ngIf="block.subtitle">{{ block.subtitle }}</ion-card-subtitle>
          </ion-card-header>
          <ion-card-content>
            <ion-list [inset]="true" class="habits-list ion-no-padding">
              <ion-item
                *ngFor="let habit of block.habits"
                button
                lines="full"
                class="habit-row"
                [class.completed]="habit.completed"
                data-habit-toggle
                (click)="onToggleHabit(habit, block.id)"
              >
                <ion-checkbox
                  slot="start"
                  [checked]="habit.completed"
                  aria-label="Marcar hábito"
                ></ion-checkbox>
                <ion-label>
                  <h3>
                    <span class="habit-time mono-font">{{ habit.time }}</span> — {{ habit.label }}
                  </h3>
                </ion-label>
                <ion-badge slot="end" [color]="habit.completed ? 'success' : 'medium'">
                  {{ habit.completed ? 'Feito' : 'Pendente' }}
                </ion-badge>
              </ion-item>
            </ion-list>
          </ion-card-content>
        </ion-card>

        <!-- 3. KANBAN BOARD BLOCK -->
        <ion-card
          *ngIf="block.type === 'kanban-board' && block.kanban"
          class="ui-card kanban-card"
          data-block-type="kanban-board"
        >
          <ion-card-header>
            <div class="card-header-row">
              <ion-card-title>{{ block.title }} ({{ block.kanban.boardName }})</ion-card-title>
              <ion-badge color="tertiary">Kanban</ion-badge>
            </div>
            <ion-card-subtitle *ngIf="block.subtitle">{{ block.subtitle }}</ion-card-subtitle>
          </ion-card-header>
          <ion-card-content>
            <div class="kanban-columns">
              <div *ngFor="let col of block.kanban.columns" class="kanban-column">
                <div class="column-header">
                  <h4>{{ col.name }}</h4>
                  <ion-badge color="dark">{{ col.tasks.length }}</ion-badge>
                </div>
                <div class="task-list">
                  <ion-card *ngFor="let task of col.tasks" class="task-card ion-no-margin">
                    <ion-card-content class="task-content">
                      <span class="task-text">{{ task.text }}</span>
                      <ion-button
                        size="small"
                        fill="outline"
                        color="primary"
                        data-kanban-move
                        (click)="onMoveKanbanTask(block.kanban.boardName, task, col.name, block.kanban.columns, block.id); $event.stopPropagation();"
                      >
                        Mover →
                      </ion-button>
                    </ion-card-content>
                  </ion-card>
                </div>
              </div>
            </div>
          </ion-card-content>
        </ion-card>

        <!-- 4. DYNAMIC FORM BLOCK -->
        <ion-card
          *ngIf="block.type === 'dynamic-form' && block.form"
          class="ui-card dynamic-form-card"
          data-block-type="dynamic-form"
        >
          <ion-card-header>
            <div class="card-header-row">
              <ion-card-title>{{ block.title }}</ion-card-title>
              <ion-badge color="warning">Formulário</ion-badge>
            </div>
            <ion-card-subtitle *ngIf="block.subtitle">{{ block.subtitle }}</ion-card-subtitle>
          </ion-card-header>
          <ion-card-content>
            <form (ngSubmit)="onSubmitForm(block)" class="form-container">
              <div *ngFor="let field of block.form.fields" class="form-group">
                <ion-item fill="outline" class="form-item">
                  <ion-label position="stacked">
                    {{ field.label }}
                    <span *ngIf="field.required" class="required-star">*</span>
                  </ion-label>

                  <!-- Text Field -->
                  <ion-input
                    *ngIf="field.fieldType === 'text' || field.fieldType === 'date'"
                    [type]="field.fieldType === 'date' ? 'date' : 'text'"
                    [placeholder]="field.placeholder || ''"
                    [(ngModel)]="getFormStore(block.id)[field.key]"
                    [name]="block.id + '_' + field.key"
                    [required]="!!field.required"
                  ></ion-input>

                  <!-- Textarea Field -->
                  <ion-textarea
                    *ngIf="field.fieldType === 'textarea'"
                    rows="3"
                    [placeholder]="field.placeholder || ''"
                    [(ngModel)]="getFormStore(block.id)[field.key]"
                    [name]="block.id + '_' + field.key"
                    [required]="!!field.required"
                  ></ion-textarea>

                  <!-- Select Field -->
                  <ion-select
                    *ngIf="field.fieldType === 'select'"
                    [(ngModel)]="getFormStore(block.id)[field.key]"
                    [name]="block.id + '_' + field.key"
                  >
                    <ion-select-option *ngFor="let opt of field.options" [value]="opt">{{ opt }}</ion-select-option>
                  </ion-select>
                </ion-item>
              </div>

              <ion-button type="submit" color="success" data-form-submit (click)="onSubmitForm(block)">
                <ion-icon slot="start" name="save-outline"></ion-icon>
                {{ block.form.submitLabel || 'Salvar' }}
              </ion-button>
            </form>
          </ion-card-content>
        </ion-card>

        <!-- 5. MARKDOWN NOTE LIST BLOCK -->
        <ion-card
          *ngIf="block.type === 'markdown-note-list' && block.notes"
          class="ui-card note-list-card"
          data-block-type="markdown-note-list"
        >
          <ion-card-header>
            <div class="card-header-row">
              <ion-card-title>{{ block.title }}</ion-card-title>
              <ion-badge color="medium">Zettelkasten</ion-badge>
            </div>
            <ion-card-subtitle *ngIf="block.subtitle">{{ block.subtitle }}</ion-card-subtitle>
          </ion-card-header>
          <ion-card-content>
            <div class="notes-grid">
              <ion-card
                *ngFor="let note of block.notes"
                button
                class="note-inner-card ion-no-margin"
                (click)="onSelectNote(note, block.id)"
              >
                <ion-card-header>
                  <div class="card-header-row">
                    <ion-card-title class="note-title">{{ note.title }}</ion-card-title>
                    <ion-badge color="dark" class="mono-font">{{ note.category }}</ion-badge>
                  </div>
                </ion-card-header>
                <ion-card-content>
                  <p *ngIf="note.excerpt" class="note-excerpt">{{ note.excerpt }}</p>
                  <div *ngIf="note.tags && note.tags.length" class="tags-container">
                    <ion-chip *ngFor="let tag of note.tags" color="success">
                      #{{ tag }}
                    </ion-chip>
                  </div>
                </ion-card-content>
              </ion-card>
            </div>
          </ion-card-content>
        </ion-card>

      </div>
    </div>
  `,
  styles: [`
    .generative-canvas {
      display: flex;
      flex-direction: column;
      gap: 1.25rem;
      width: 100%;
      margin: 0.5rem 0;
    }
    .ui-card {
      --background: #161b22;
      border: 1px solid #30363d;
      border-radius: 8px;
      box-shadow: 0 4px 12px rgba(0, 0, 0, 0.25);
    }
    .card-header-row {
      display: flex;
      justify-content: space-between;
      align-items: center;
      margin-bottom: 0.25rem;
    }

    /* KPI Grid */
    .kpi-box {
      background: #0d1117;
      border: 1px solid #21262d;
      border-radius: 6px;
      padding: 0.85rem;
      border-left: 4px solid #58a6ff;
      margin: 0.3rem;
    }
    .kpi-box.accent-secondary { border-left-color: #7ee787; }
    .kpi-box.accent-emerald { border-left-color: #7ee787; }
    .kpi-box.accent-indigo { border-left-color: #58a6ff; }
    .kpi-box.accent-cyan { border-left-color: #79c0ff; }
    .kpi-box.accent-amber { border-left-color: #f2cc60; }
    .kpi-box.accent-warning { border-left-color: #f2cc60; }
    .kpi-box.accent-danger { border-left-color: #f85149; }
    .kpi-label { font-size: 0.75rem; color: #8b949e; text-transform: uppercase; }
    .kpi-value { font-size: 1.5rem; font-weight: bold; color: #f0f6fc; margin: 0.2rem 0; }
    .kpi-sub { font-size: 0.75rem; color: #8b949e; }

    /* Habits */
    .habits-list {
      background: transparent;
    }
    .habit-row {
      --background: #0d1117;
      --border-color: #21262d;
      border-radius: 6px;
      margin-bottom: 0.4rem;
    }
    .habit-row.completed {
      opacity: 0.75;
      --border-color: #238636;
    }
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
    .task-list { display: flex; flex-direction: column; gap: 0.5rem; }
    .task-card {
      --background: #161b22;
      border: 1px solid #30363d;
      border-radius: 6px;
    }
    .task-content {
      padding: 0.6rem;
      display: flex;
      justify-content: space-between;
      align-items: center;
      gap: 0.5rem;
    }
    .task-text { font-size: 0.85rem; color: #f0f6fc; }

    /* Dynamic Form */
    .form-container { display: flex; flex-direction: column; gap: 0.75rem; }
    .form-group { display: flex; flex-direction: column; }
    .form-item {
      --background: #0d1117;
      --border-color: #30363d;
      border-radius: 6px;
    }
    .required-star { color: #f85149; }

    /* Notes */
    .notes-grid { display: flex; flex-direction: column; gap: 0.75rem; }
    .note-inner-card {
      --background: #0d1117;
      border: 1px solid #21262d;
      border-radius: 6px;
    }
    .note-title { font-size: 0.95rem; font-weight: 600; color: #58a6ff; }
    .note-excerpt { font-size: 0.85rem; color: #8b949e; margin: 0 0 0.5rem 0; }
    .tags-container { display: flex; gap: 0.3rem; flex-wrap: wrap; }
  `]
})
export class GenerativeCanvasComponent implements OnInit {
  @Input() public blocks: GenerativeUiBlock[] = [];

  @Output() public habitToggled = new EventEmitter<{ habit: HabitItem; blockId: string }>();
  @Output() public kanbanTaskMoved = new EventEmitter<{ boardName: string; taskText: string; targetColumn: string; blockId: string }>();
  @Output() public dynamicFormSubmitted = new EventEmitter<{ formId: string; noteType: string; values: Record<string, string>; blockId: string }>();
  @Output() public noteSelected = new EventEmitter<{ note: VaultNoteSummary; blockId: string }>();

  public formStore: Record<string, Record<string, string>> = {};

  constructor() {
    addIcons({
      checkmarkCircle,
      arrowForwardOutline,
      saveOutline,
      documentTextOutline,
      listOutline,
      gridOutline,
      calendarOutline,
      sparklesOutline,
      timeOutline
    });
  }

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
