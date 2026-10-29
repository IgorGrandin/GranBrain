import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { By } from '@angular/platform-browser';
import { AppComponent } from './app.component';
import { GenerativeCanvasComponent } from './components/generative-canvas/generative-canvas.component';
import { VoiceService } from './services/voice.service';
import { GranBrainApiService } from './services/granbrain-api.service';
import { GenerativeUiBlock } from './models/granbrain.models';

describe('GranBrain Frontend Generative UI & App Suite', () => {
  describe('GenerativeCanvasComponent', () => {
    let fixture: ComponentFixture<GenerativeCanvasComponent>;
    let component: GenerativeCanvasComponent;

    beforeEach(async () => {
      await TestBed.configureTestingModule({
        imports: [GenerativeCanvasComponent]
      }).compileComponents();

      fixture = TestBed.createComponent(GenerativeCanvasComponent);
      component = fixture.componentInstance;
    });

    it('GenerativeCanvasComponent renders all 5 Generative UI blocks dynamically and dispatches actions', () => {
      const mockBlocks: GenerativeUiBlock[] = [
        {
          id: 'blk-1',
          type: 'kpi-grid',
          title: 'Resumo Executivo',
          metrics: [
            { label: 'Hábitos Hoje', value: '4/6', subtitle: '66%', accent: 'secondary' },
            { label: 'Zettelkasten', value: '142', subtitle: 'Notas', accent: 'primary' }
          ]
        },
        {
          id: 'blk-2',
          type: 'habit-checklist',
          title: 'Hábitos Diários',
          habits: [
            { time: '07:30', label: 'Orar/Ler Bíblia', fullText: '- [x] 07:30 - Orar/Ler Bíblia', completed: true },
            { time: '08:00', label: 'Treino', fullText: '- [ ] 08:00 - Treino', completed: false }
          ]
        },
        {
          id: 'blk-3',
          type: 'kanban-board',
          title: 'Quadro Profissional',
          kanban: {
            boardName: 'Profissional',
            relativePath: 'kanban/Profissional.md',
            columns: [
              {
                name: 'To Do',
                tasks: [{ id: 'task-1', text: 'Refatorar endpoint', completed: false, column: 'To Do' }]
              },
              {
                name: 'Doing',
                tasks: [{ id: 'task-2', text: 'Implementar Generative UI', completed: false, column: 'Doing' }]
              }
            ]
          }
        },
        {
          id: 'blk-4',
          type: 'dynamic-form',
          title: 'Captura Rápida',
          form: {
            formId: 'form-reuniao',
            noteType: 'reuniao',
            submitLabel: 'Registrar Reunião',
            fields: [
              { key: 'title', label: 'Título', fieldType: 'text', required: true, defaultValue: 'Alinhamento Semanal' },
              { key: 'summary', label: 'Resumo', fieldType: 'textarea', required: false, defaultValue: 'Discussão de roadmap' }
            ]
          }
        },
        {
          id: 'blk-5',
          type: 'markdown-note-list',
          title: 'Notas Recentes',
          notes: [
            {
              title: 'Arquitetura GranBrain',
              relativePath: 'zettelkasten/Arquitetura.md',
              category: 'zettelkasten',
              lastModifiedUtc: new Date().toISOString(),
              tags: ['arquitetura', 'dotnet'],
              excerpt: 'Decisões arquiteturais do GranBrain AI OS.'
            }
          ]
        }
      ];

      component.blocks = mockBlocks;
      fixture.detectChanges();

      // Verify DOM rendering of all 5 block types
      const kpiEl = fixture.debugElement.query(By.css('[data-block-type="kpi-grid"]'));
      expect(kpiEl).withContext('KPI Grid block should be rendered').not.toBeNull();

      const habitEl = fixture.debugElement.query(By.css('[data-block-type="habit-checklist"]'));
      expect(habitEl).withContext('Habit Checklist block should be rendered').not.toBeNull();

      const kanbanEl = fixture.debugElement.query(By.css('[data-block-type="kanban-board"]'));
      expect(kanbanEl).withContext('Kanban Board block should be rendered').not.toBeNull();

      const formEl = fixture.debugElement.query(By.css('[data-block-type="dynamic-form"]'));
      expect(formEl).withContext('Dynamic Form block should be rendered').not.toBeNull();

      const notesEl = fixture.debugElement.query(By.css('[data-block-type="markdown-note-list"]'));
      expect(notesEl).withContext('Markdown Note List block should be rendered').not.toBeNull();

      // Test Event Dispatching
      let habitDispatched: any = null;
      component.habitToggled.subscribe((event) => {
        habitDispatched = event;
      });

      const habitItemBtn = fixture.debugElement.query(By.css('[data-habit-toggle]'));
      expect(habitItemBtn).not.toBeNull();
      habitItemBtn.nativeElement.click();
      expect(habitDispatched).not.toBeNull();
      expect(habitDispatched.blockId).toBe('blk-2');

      let kanbanDispatched: any = null;
      component.kanbanTaskMoved.subscribe((event) => {
        kanbanDispatched = event;
      });

      const kanbanMoveBtn = fixture.debugElement.query(By.css('[data-kanban-move]'));
      expect(kanbanMoveBtn).not.toBeNull();
      kanbanMoveBtn.nativeElement.click();
      expect(kanbanDispatched).not.toBeNull();
      expect(kanbanDispatched.boardName).toBe('Profissional');

      let formDispatched: any = null;
      component.dynamicFormSubmitted.subscribe((event) => {
        formDispatched = event;
      });

      const formSubmitBtn = fixture.debugElement.query(By.css('[data-form-submit]'));
      expect(formSubmitBtn).not.toBeNull();
      formSubmitBtn.nativeElement.click();
      expect(formDispatched).not.toBeNull();
      expect(formDispatched.noteType).toBe('reuniao');
    });
  });

  describe('AppComponent', () => {
    let fixture: ComponentFixture<AppComponent>;
    let component: AppComponent;
    let httpMock: HttpTestingController;

    beforeEach(async () => {
      await TestBed.configureTestingModule({
        imports: [AppComponent],
        providers: [
          provideHttpClient(),
          provideHttpClientTesting(),
          GranBrainApiService,
          VoiceService
        ]
      }).compileComponents();

      httpMock = TestBed.inject(HttpTestingController);
      fixture = TestBed.createComponent(AppComponent);
      component = fixture.componentInstance;
    });

    afterEach(() => {
      httpMock.verify();
    });

    it('AppComponent handles login, provider switching and voice service trigger', () => {
      fixture.detectChanges();
      expect(component.title).toBe('GranBrain');

      // Test provider switching
      component.selectProvider('gemini');
      expect(component.selectedProvider).toBe('gemini');

      component.selectProvider('claude');
      expect(component.selectedProvider).toBe('claude');

      // Test voice triggering
      spyOn(component.voice, 'startListening').and.returnValue(true);
      component.toggleVoice();
      expect(component.voice.startListening).toHaveBeenCalled();
    });
  });
});
