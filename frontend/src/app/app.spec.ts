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

    it('AppComponent navigates to Ideas 0002 view and executes idea capture, brainstorm and promotion flows', () => {
      fixture.detectChanges();
      component.isAuthenticated = true;
      fixture.detectChanges();

      const ideasNav = fixture.debugElement.queryAll(By.css('.rail .nav-item'))
        .find((el) => el.nativeElement.textContent.includes('0002'));
      expect(ideasNav).withContext('Should render 0002 nav item in rail').toBeTruthy();

      ideasNav!.nativeElement.click();
      const overviewReq = httpMock.expectOne('/api/vault/ideas/overview');
      expect(overviewReq.request.method).toBe('GET');
      overviewReq.flush({
        ideas: [
          { id: 'idea-1', text: 'Motor de ideias', time: '09:00', status: 'fleeting', sourcePath: 'zettelkasten/Fleeting.md' }
        ],
        fleetingCount: 1,
        brainstormCount: 0,
        promotedCount: 0,
        discardedCount: 0,
        activeSession: null
      });

      fixture.detectChanges();
      expect((component as any).currentView).toBe('ideas');
      expect(component.currentBlocks.some((b) => b.type === 'idea-capture')).toBeTrue();
      expect(component.currentBlocks.some((b) => b.type === 'brainstorm-canvas')).toBeTrue();

      (component as any).onIdeaCaptured({ text: 'Nova ideia', target: 'fleeting', blockId: 'blk-idea-cap' });
      const capReq = httpMock.expectOne('/api/vault/ideas/fleeting');
      expect(capReq.request.method).toBe('POST');
      capReq.flush({ id: 'idea-2', text: 'Nova ideia', time: '09:05', status: 'fleeting', sourcePath: 'zettelkasten/Fleeting.md' });
      httpMock.expectOne('/api/vault/ideas/overview').flush({
        ideas: [],
        fleetingCount: 2,
        brainstormCount: 0,
        promotedCount: 0,
        discardedCount: 0,
        activeSession: null
      });

      (component as any).onIdeaBrainstormRequested({ ideaId: 'idea-2', ideaText: 'Nova ideia', blockId: 'blk-bs' });
      const bsReq = httpMock.expectOne('/api/vault/ideas/brainstorm');
      expect(bsReq.request.method).toBe('POST');
      bsReq.flush({
        idea: { id: 'idea-2', text: 'Nova ideia', time: '09:05', status: 'brainstorm', sourcePath: 'zettelkasten/Fleeting.md' },
        notePath: 'zettelkasten/Brainstorming - Nova ideia.md',
        problem: 'Problema central',
        hypotheses: [{ text: 'H1', confidence: 'alta' }],
        risks: [{ text: 'R1', severity: 'alto' }],
        socraticQuestion: 'Por que agora?'
      });
      httpMock.expectOne('/api/vault/ideas/overview').flush({
        ideas: [],
        fleetingCount: 1,
        brainstormCount: 1,
        promotedCount: 0,
        discardedCount: 0,
        activeSession: null
      });

      (component as any).onIdeaPromoted({ ideaId: 'idea-2', target: 'permanente', socraticAnswer: 'Porque sim', blockId: 'blk-bs' });
      const promReq = httpMock.expectOne('/api/vault/ideas/promote');
      expect(promReq.request.method).toBe('POST');
      promReq.flush({
        idea: { id: 'idea-2', text: 'Nova ideia', time: '09:05', status: 'promovida', sourcePath: 'zettelkasten/Fleeting.md' },
        originPath: 'zettelkasten/Brainstorming - Nova ideia.md',
        targetPath: 'zettelkasten/Nova ideia.md',
        status: 'promovida'
      });
      httpMock.expectOne('/api/vault/ideas/overview').flush({
        ideas: [],
        fleetingCount: 1,
        brainstormCount: 0,
        promotedCount: 1,
        discardedCount: 0,
        activeSession: null
      });

      fixture.detectChanges();
      const toastEl = fixture.debugElement.query(By.css('.toasts .toast'));
      expect(toastEl).withContext('Should show toast feedback').not.toBeNull();
    });
  });

  describe('GenerativeCanvasComponent Ideas & Brainstorm Blocks', () => {
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

    it('GenerativeCanvasComponent renders idea-capture and brainstorm-canvas blocks and dispatches idea actions', async () => {
      const overview: any = {
        ideas: [
          { id: 'i1', text: 'Ideia fleeting', time: '08:10', status: 'fleeting', sourcePath: 'zettelkasten/Fleeting.md' },
          { id: 'i2', text: 'Ideia em brainstorm', time: '08:20', status: 'brainstorm', sourcePath: 'zettelkasten/Fleeting.md' },
          { id: 'i3', text: 'Ideia promovida', time: '08:30', status: 'promovida', sourcePath: 'zettelkasten/Fleeting.md' },
          { id: 'i4', text: 'Ideia descartada', time: '08:40', status: 'descartada', sourcePath: 'zettelkasten/Fleeting.md' }
        ],
        fleetingCount: 1,
        brainstormCount: 1,
        promotedCount: 1,
        discardedCount: 1,
        activeSession: {
          idea: { id: 'i2', text: 'Ideia em brainstorm', time: '08:20', status: 'brainstorm', sourcePath: 'zettelkasten/Fleeting.md' },
          notePath: 'zettelkasten/Brainstorming - Ideia em brainstorm.md',
          problem: 'Falta de validação rápida',
          hypotheses: [
            { text: 'Validação reduz retrabalho', confidence: 'alta' },
            { text: 'Template único acelera', confidence: 'média' }
          ],
          risks: [
            { text: 'Escopo crescer demais', severity: 'alto' },
            { text: 'Pouco uso diário', severity: 'baixo' }
          ],
          socraticQuestion: 'Qual evidência prova que isso resolve o problema?'
        }
      };

      component.blocks = [
        { id: 'b-cap', type: 'idea-capture' as any, title: 'Captura', ideasOverview: overview } as any,
        { id: 'b-bs', type: 'brainstorm-canvas' as any, title: 'Brainstorm', ideasOverview: overview } as any
      ];
      fixture.detectChanges();

      const capBlock = fixture.debugElement.query(By.css('[data-block-type="idea-capture"]'));
      expect(capBlock).not.toBeNull();
      expect(capBlock.query(By.css('.input.big-in'))).not.toBeNull();
      expect(capBlock.query(By.css('.btn.icon.mic'))).not.toBeNull();
      expect(capBlock.query(By.css('.hint'))).not.toBeNull();

      const bsBlock = fixture.debugElement.query(By.css('[data-block-type="brainstorm-canvas"]'));
      expect(bsBlock).not.toBeNull();
      const stagePills = bsBlock.queryAll(By.css('.stages .pill'));
      expect(stagePills.length).toBe(4);
      expect(bsBlock.query(By.css('.stages .pill.info'))).not.toBeNull();
      expect(bsBlock.query(By.css('.stages .pill.accent'))).not.toBeNull();
      expect(bsBlock.query(By.css('.stages .pill.ok'))).not.toBeNull();

      const ideaRows = bsBlock.queryAll(By.css('.idea'));
      expect(ideaRows.length).toBe(4);
      expect(ideaRows[0].query(By.css('[data-idea-brainstorm]'))).not.toBeNull();
      expect(ideaRows[1].query(By.css('[data-idea-brainstorm]'))).toBeNull();
      expect(ideaRows[2].query(By.css('.acts'))).toBeNull();
      expect(ideaRows[3].query(By.css('.acts'))).toBeNull();

      expect(bsBlock.query(By.css('.session-h'))).not.toBeNull();
      const board3El = bsBlock.query(By.css('.board3'));
      expect(board3El).not.toBeNull();
      expect(board3El.queryAll(By.css('.hyp')).length).toBe(4);

      const qEl = bsBlock.query(By.css('.socratic .q'));
      expect(qEl).not.toBeNull();
      expect(bsBlock.query(By.css('.bar-actions'))).not.toBeNull();

      // Computed style checks against mockup
      const board3Style = getComputedStyle(board3El.nativeElement);
      const ideaStyle = getComputedStyle(ideaRows[0].nativeElement);
      const qStyle = getComputedStyle(qEl.nativeElement);
      expect(board3Style.display).toBe('grid');
      expect(ideaStyle.display).toBe('flex');
      expect(qStyle.fontSize).toBe('21px');
      expect(qStyle.fontFamily).toContain('Bricolage Grotesque');

      // Event emissions
      const capSpy = spyOn((component as any).ideaCaptured, 'emit');
      const voiceSpy = spyOn((component as any).ideaVoiceTriggered, 'emit');
      const bsSpy = spyOn((component as any).ideaBrainstormRequested, 'emit');
      const promSpy = spyOn((component as any).ideaPromoted, 'emit');

      await fixture.whenStable();
      const inputEl = capBlock.query(By.css('[data-idea-input]')).nativeElement as HTMLInputElement;
      inputEl.value = 'Ideia digitada';
      inputEl.dispatchEvent(new Event('input', { bubbles: true }));
      fixture.detectChanges();
      capBlock.query(By.css('form.quick-form')).triggerEventHandler('ngSubmit', null);
      expect(capSpy).toHaveBeenCalledWith({ text: 'Ideia digitada', target: 'fleeting', blockId: 'b-cap' });

      capBlock.query(By.css('[data-idea-voice]')).nativeElement.click();
      expect(voiceSpy).toHaveBeenCalledWith({ blockId: 'b-cap' });

      ideaRows[0].query(By.css('[data-idea-brainstorm]')).nativeElement.click();
      expect(bsSpy).toHaveBeenCalledWith({ ideaId: 'i1', ideaText: 'Ideia fleeting', blockId: 'b-bs' });

      bsBlock.query(By.css('[data-session-promote="permanente"]')).nativeElement.click();
      expect(promSpy).toHaveBeenCalled();
    });
  });
});
