import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { provideIonicAngular } from '@ionic/angular/standalone';
import { By } from '@angular/platform-browser';
import { AppComponent } from './app.component';
import { GenerativeCanvasComponent } from './components/generative-canvas/generative-canvas.component';
import { VoiceService } from './services/voice.service';
import { GranBrainApiService } from './services/granbrain-api.service';
import { GenerativeUiBlock } from './models/granbrain.models';

describe('GranBrain Frontend Generative UI & App Suite', () => {
  describe('GranBrain Design Tokens & Typography', () => {
    afterEach(() => {
      document.documentElement.removeAttribute('data-theme');
    });

    it('defines canonical design tokens in light and dark mode', () => {
      const computed = getComputedStyle(document.documentElement);
      const fontDisplay = computed.getPropertyValue('--font-display').trim();
      const fontBody = computed.getPropertyValue('--font-body').trim();
      const fontMono = computed.getPropertyValue('--font-mono').trim();
      const accent = computed.getPropertyValue('--accent').trim();

      expect(fontDisplay).toContain('Bricolage Grotesque');
      expect(fontBody).toContain('IBM Plex Sans');
      expect(fontMono).toContain('JetBrains Mono');
      expect(accent).toBe('#0c7766');

      document.documentElement.setAttribute('data-theme', 'dark');
      const darkComputed = getComputedStyle(document.documentElement);
      const darkAccent = darkComputed.getPropertyValue('--accent').trim();
      const darkBg = darkComputed.getPropertyValue('--bg').trim();

      expect(darkAccent).toBe('#51d0b3');
      expect(darkBg).toBe('#0b1210');
    });
  });

  describe('GenerativeCanvasComponent', () => {
    let fixture: ComponentFixture<GenerativeCanvasComponent>;
    let component: GenerativeCanvasComponent;

    beforeEach(async () => {
      await TestBed.configureTestingModule({
        imports: [GenerativeCanvasComponent],
        providers: [provideIonicAngular()]
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

      // Redesign Design Classes checks
      const stripEl = fixture.debugElement.query(By.css('.strip .cell'));
      expect(stripEl).withContext('Should render .strip .cell in KPI Grid').not.toBeNull();

      const habitCheckEl = fixture.debugElement.query(By.css('.habit .check'));
      expect(habitCheckEl).withContext('Should render .habit .check in Habit Checklist').not.toBeNull();

      const kanbanColEl = fixture.debugElement.query(By.css('.board .col'));
      expect(kanbanColEl).withContext('Should render .board .col in Kanban').not.toBeNull();

      const notesSplitEl = fixture.debugElement.query(By.css('.notes-split'));
      expect(notesSplitEl).withContext('Should render .notes-split in Zettelkasten Notes').not.toBeNull();

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
          provideIonicAngular(),
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

      // Test initial model is lightweight default (gpt-6-luna-medium)
      expect(component.selectedProvider).toBe('codex');
      expect(component.activeModel).toBe('gpt-6-luna-medium');

      // Test provider switching and active model updates
      component.selectProvider('gemini');
      expect(component.selectedProvider).toBe('gemini');
      expect(component.activeModel).toBe('gemini-3.8-flash-medium');

      component.selectProvider('claude');
      expect(component.selectedProvider).toBe('claude');
      expect(component.activeModel).toBe('claude-haiku-5-5-medium');

      // Test voice triggering
      spyOn(component.voice, 'startListening').and.returnValue(true);
      component.toggleVoice();
      expect(component.voice.startListening).toHaveBeenCalled();

      // Test onHabitToggled with array response from backend
      const habitItem = { time: '07:30', label: 'Orar/Ler Bíblia', fullText: '- [ ] 07:30 - Orar/Ler Bíblia', completed: false };
      spyOn(component.api, 'toggleHabit').and.returnValue(
        jasmine.createSpyObj('Observable', ['subscribe'])
      );
      (component.api.toggleHabit as any).and.callFake(() => ({
        subscribe: (callbacks: any) => {
          callbacks.next([
            { time: '07:30', label: 'Orar/Ler Bíblia', fullText: '- [x] 07:30 - Orar/Ler Bíblia', completed: true }
          ]);
        }
      }));

      component.onHabitToggled({ habit: habitItem, blockId: 'blk-habits' });
      expect(habitItem.completed).toBeTrue();
    });

    it('AppComponent Login Split-Screen & Error Handling renders .login-art with SVG graph and displays .err on failure', () => {
      component.isAuthenticated = false;
      fixture.detectChanges();

      const loginEl = fixture.debugElement.query(By.css('.login'));
      expect(loginEl).withContext('Should render .login container').not.toBeNull();

      const loginArt = fixture.debugElement.query(By.css('.login-art'));
      expect(loginArt).withContext('Should render .login-art container').not.toBeNull();

      const graphSvg = fixture.debugElement.query(By.css('.login-art svg.graph'));
      expect(graphSvg).withContext('Should render SVG Knowledge Graph').not.toBeNull();

      const loginForm = fixture.debugElement.query(By.css('.login-form'));
      expect(loginForm).withContext('Should render .login-form container').not.toBeNull();

      // Trigger invalid login to test .err container
      component.loginError = 'Credenciais inválidas';
      fixture.detectChanges();

      const errEl = fixture.debugElement.query(By.css('.err'));
      expect(errEl).withContext('Should display error in .err container').not.toBeNull();
      expect(errEl.nativeElement.textContent).toContain('Credenciais inválidas');

      // Test toast notification system
      (component as any).showToast('Nota salva no cofre', 'ok');
      fixture.detectChanges();

      const toastEl = fixture.debugElement.query(By.css('.toasts .toast'));
      expect(toastEl).withContext('Should render notification in .toasts .toast').not.toBeNull();
      expect(toastEl.nativeElement.textContent).toContain('Nota salva no cofre');
    });

    it('AppComponent Shell Rail & Live Voice Pulse renders .rail, .topbar, .wi badges and pulses mic when listening', () => {
      fixture.detectChanges();
      component.isAuthenticated = true;
      fixture.detectChanges();

      const appEl = fixture.debugElement.query(By.css('.app'));
      expect(appEl).withContext('Should render .app shell').not.toBeNull();

      const railEl = fixture.debugElement.query(By.css('.rail'));
      expect(railEl).withContext('Should render .rail sidebar').not.toBeNull();

      const wiEl = fixture.debugElement.query(By.css('.rail .wi'));
      expect(wiEl).withContext('Should render .wi badge in rail navigation').not.toBeNull();
      expect(wiEl.nativeElement.textContent).toContain('0001');

      const topbarEl = fixture.debugElement.query(By.css('.topbar'));
      expect(topbarEl).withContext('Should render .topbar header').not.toBeNull();

      const crumbEl = fixture.debugElement.query(By.css('.topbar .crumb'));
      expect(crumbEl).withContext('Should render .crumb breadcrumb').not.toBeNull();

      const userChipEl = fixture.debugElement.query(By.css('.topbar .user-chip'));
      expect(userChipEl).withContext('Should render .user-chip in topbar').not.toBeNull();

      // Test mic live pulse
      component.isListening = false;
      fixture.detectChanges();
      const micBtnIdle = fixture.debugElement.query(By.css('button[title*="Ativar voz"]'));
      expect(micBtnIdle).not.toBeNull();
      expect(micBtnIdle.nativeElement.classList.contains('live')).toBeFalse();

      component.isListening = true;
      fixture.detectChanges();
      const micBtnLive = fixture.debugElement.query(By.css('.btn.mic.live'));
      expect(micBtnLive).withContext('Should add .btn.mic.live class when listening').not.toBeNull();
    });
  });
});
