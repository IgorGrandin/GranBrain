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

      // Voice dictation from idea-capture routes directly to /api/vault/ideas/fleeting
      spyOn(component.voice, 'startListening').and.returnValue(true);
      component.onIdeaVoiceTriggered({ blockId: 'blk-idea-cap' });
      (component.voice as any).transcriptSubject.next('Ideia ditada por voz');
      const voiceCapReq = httpMock.expectOne('/api/vault/ideas/fleeting');
      expect(voiceCapReq.request.body.text).toBe('Ideia ditada por voz');
      voiceCapReq.flush({ id: 'idea-3', text: 'Ideia ditada por voz', time: '09:10', status: 'fleeting', sourcePath: 'zettelkasten/Fleeting.md' });
      httpMock.expectOne('/api/vault/ideas/overview').flush({
        ideas: [],
        fleetingCount: 2,
        brainstormCount: 0,
        promotedCount: 1,
        discardedCount: 0,
        activeSession: null
      });
    });

    it('AppComponent navigates to Agenda 0003 view via rail, syncs calendar, and creates meeting notes', () => {
      fixture.detectChanges();
      component.isAuthenticated = true;
      fixture.detectChanges();

      const agendaNav = fixture.debugElement.queryAll(By.css('.rail .nav-item'))
        .find((el) => el.nativeElement.textContent.includes('0003'));
      expect(agendaNav).withContext('Should render 0003 nav item in rail').toBeTruthy();

      const sampleAgenda = {
        date: '2026-10-30',
        timeZone: 'America/Sao_Paulo',
        lastSyncedAt: '2026-10-30T10:00:00Z',
        conflictsCount: 1,
        providersConnected: ['Google', 'Teams', 'Outlook'],
        events: [
          {
            id: 'ev-teams-1',
            provider: 'Teams',
            title: 'Arquitetura GranBrain',
            startIso: '2026-10-30T10:00:00-03:00',
            endIso: '2026-10-30T11:00:00-03:00',
            startTime: '10:00',
            endTime: '11:00',
            durationMinutes: 60,
            location: 'Microsoft Teams',
            attendees: ['igor@granbrain.dev'],
            joinUrl: 'https://teams.microsoft.com/l/meetup-join/123',
            notePath: null,
            status: 'now',
            hasConflict: false,
            calendarTopPx: 120,
            calendarHeightPx: 60,
            calendarColumn: 0,
            calendarTotalColumns: 1
          }
        ]
      };

      agendaNav!.nativeElement.click();
      const todayReq = httpMock.expectOne('/api/calendar/today');
      expect(todayReq.request.method).toBe('GET');
      todayReq.flush(sampleAgenda);

      fixture.detectChanges();
      expect((component as any).currentView).toBe('agenda');
      expect(fixture.debugElement.query(By.css('.topbar .crumb')).nativeElement.textContent).toContain('Agenda');
      expect(component.currentBlocks.some((b) => b.type === 'agenda-timeline')).toBeTrue();

      const syncBtn = fixture.debugElement.query(By.css('[data-agenda-sync]'));
      expect(syncBtn).withContext('Should render sync button for agenda-timeline').not.toBeNull();
      syncBtn.nativeElement.click();

      const syncReq = httpMock.expectOne('/api/calendar/sync');
      expect(syncReq.request.method).toBe('POST');
      syncReq.flush({
        date: '2026-10-30',
        dailyNotePath: 'diario/2026-10-30.md',
        syncedCount: 1,
        conflictsCount: 0,
        createdNotes: [],
        agenda: sampleAgenda
      });
      fixture.detectChanges();

      const noteBtn = fixture.debugElement.query(By.css('[data-event-note]'));
      expect(noteBtn).withContext('Should render create-note action button on event card').not.toBeNull();
      noteBtn.nativeElement.click();

      const noteReq = httpMock.expectOne('/api/calendar/events/ev-teams-1/note');
      expect(noteReq.request.method).toBe('POST');
      noteReq.flush({
        eventId: 'ev-teams-1',
        title: 'Arquitetura GranBrain',
        notePath: 'reunioes/2026-10-30 - Arquitetura GranBrain.md',
        created: true
      });
      fixture.detectChanges();

      const toastEl = fixture.debugElement.query(By.css('.toasts .toast'));
      expect(toastEl).withContext('Should show toast feedback after creating meeting note').not.toBeNull();
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

    it('renders agenda-timeline block in Lista and Calendario modes with provider filter, canonical computed styles, and actions', () => {
      const mockAgendaBlock: GenerativeUiBlock = {
        id: 'blk-agenda',
        type: 'agenda-timeline',
        title: 'Agenda do Dia',
        subtitle: 'Sexta, 30 Out · America/Sao_Paulo',
        calendarAgenda: {
          date: '2026-10-30',
          timeZone: 'America/Sao_Paulo',
          lastSyncedAt: '2026-10-30T10:15:00Z',
          conflictsCount: 2,
          providersConnected: ['Google', 'Teams', 'Outlook'],
          events: [
            {
              id: 'ev-g-past',
              provider: 'Google',
              title: 'Daily Standup',
              startIso: '2026-10-30T08:30:00-03:00',
              endIso: '2026-10-30T09:00:00-03:00',
              startTime: '08:30',
              endTime: '09:00',
              durationMinutes: 30,
              location: 'Google Meet',
              attendees: ['ana@granbrain.dev'],
              joinUrl: 'https://meet.google.com/abc-defg-hij',
              notePath: null,
              status: 'past',
              hasConflict: false,
              calendarTopPx: 30,
              calendarHeightPx: 30,
              calendarColumn: 0,
              calendarTotalColumns: 1
            },
            {
              id: 'ev-t-now',
              provider: 'Teams',
              title: 'Review Sprint',
              startIso: '2026-10-30T10:00:00-03:00',
              endIso: '2026-10-30T11:00:00-03:00',
              startTime: '10:00',
              endTime: '11:00',
              durationMinutes: 60,
              location: 'Teams',
              attendees: ['time@granbrain.dev'],
              joinUrl: 'https://teams.microsoft.com/l/meetup-join/456',
              notePath: null,
              status: 'now',
              hasConflict: true,
              calendarTopPx: 120,
              calendarHeightPx: 60,
              calendarColumn: 0,
              calendarTotalColumns: 2
            },
            {
              id: 'ev-o-clash',
              provider: 'Outlook',
              title: '1:1 Produto',
              startIso: '2026-10-30T10:30:00-03:00',
              endIso: '2026-10-30T11:30:00-03:00',
              startTime: '10:30',
              endTime: '11:30',
              durationMinutes: 60,
              location: 'Sala 2',
              attendees: ['pm@granbrain.dev'],
              joinUrl: null,
              notePath: 'reunioes/2026-10-30 - 1-1 Produto.md',
              status: 'next',
              hasConflict: true,
              calendarTopPx: 150,
              calendarHeightPx: 60,
              calendarColumn: 1,
              calendarTotalColumns: 2
            },
            {
              id: 'ev-g-next',
              provider: 'Google',
              title: 'Design Critique',
              startIso: '2026-10-30T14:00:00-03:00',
              endIso: '2026-10-30T15:00:00-03:00',
              startTime: '14:00',
              endTime: '15:00',
              durationMinutes: 60,
              location: 'Meet',
              attendees: [],
              joinUrl: 'https://meet.google.com/xyz-1234-uvw',
              notePath: null,
              status: 'next',
              hasConflict: false,
              calendarTopPx: 360,
              calendarHeightPx: 60,
              calendarColumn: 0,
              calendarTotalColumns: 1
            }
          ]
        }
      } as any;

      component.blocks = [mockAgendaBlock];
      fixture.detectChanges();

      const blockEl = fixture.debugElement.query(By.css('.agenda-block'));
      expect(blockEl).withContext('Should render .agenda-block container').not.toBeNull();

      const conflictEl = blockEl.query(By.css('.conflict'));
      expect(conflictEl).withContext('Should render .conflict warning banner').not.toBeNull();
      expect(conflictEl.nativeElement.textContent).toContain('2');

      const timelineEl = blockEl.query(By.css('.timeline'));
      expect(timelineEl).withContext('Should start in Lista (.timeline) view').not.toBeNull();
      expect(blockEl.queryAll(By.css('.ev')).length).toBe(4);
      expect(blockEl.query(By.css('.ev.past'))).not.toBeNull();
      const nowEv = blockEl.query(By.css('.ev.now'));
      expect(nowEv).not.toBeNull();
      expect(blockEl.query(By.css('.ev.next'))).not.toBeNull();
      expect(blockEl.query(By.css('.nowline'))).not.toBeNull();
      expect(blockEl.queryAll(By.css('[data-event-join]')).length).toBe(3);

      // Canonical CSS computed checks in Lista mode
      const evStyle = getComputedStyle(nowEv.nativeElement);
      expect(evStyle.display).toBe('grid');
      expect(evStyle.gridTemplateColumns.startsWith('56px 14px')).toBeTrue();
      const nowDotStyle = getComputedStyle(nowEv.query(By.css('.dot')).nativeElement);
      expect(nowDotStyle.backgroundColor).toBe('rgb(23, 112, 58)');
      const conflictStyle = getComputedStyle(conflictEl.nativeElement);
      expect(conflictStyle.backgroundColor).not.toBe('rgba(0, 0, 0, 0)');

      // Provider filter chip reduces DOM items in Lista mode
      const chips = blockEl.queryAll(By.css('.chips .chip'));
      const teamsChip = chips.find((c) => c.nativeElement.textContent.includes('Teams'));
      const allChip = chips.find((c) => c.nativeElement.textContent.includes('Todos'));
      expect(teamsChip).toBeTruthy();
      expect(allChip).toBeTruthy();

      teamsChip!.nativeElement.click();
      fixture.detectChanges();
      expect(teamsChip!.nativeElement.getAttribute('aria-pressed')).toBe('true');
      expect(blockEl.queryAll(By.css('.ev')).length).toBe(1);

      allChip!.nativeElement.click();
      fixture.detectChanges();
      expect(blockEl.queryAll(By.css('.ev')).length).toBe(4);

      // Toggle to Calendario view (.seg)
      const segButtons = blockEl.queryAll(By.css('.seg button'));
      const calBtn = segButtons.find((b) => b.nativeElement.textContent.includes('Calendário'));
      expect(calBtn).toBeTruthy();
      calBtn!.nativeElement.click();
      fixture.detectChanges();

      expect(calBtn!.nativeElement.getAttribute('aria-pressed')).toBe('true');
      expect(blockEl.query(By.css('.calwrap'))).not.toBeNull();
      expect(blockEl.query(By.css('.cal-grid'))).not.toBeNull();
      expect(blockEl.query(By.css('.cal-now'))).not.toBeNull();
      expect(blockEl.queryAll(By.css('.cal-hour')).length).toBe(11);
      expect(blockEl.queryAll(By.css('.cal-ev')).length).toBe(4);

      // Provider & clash visual styling in Calendario mode
      const googleCalEv = blockEl.query(By.css('.cal-ev.p-google'));
      const teamsCalEv = blockEl.query(By.css('.cal-ev.p-teams'));
      const outlookCalEv = blockEl.query(By.css('.cal-ev.p-outlook'));
      const clashCalEv = blockEl.query(By.css('.cal-ev.clash'));
      expect(googleCalEv).not.toBeNull();
      expect(teamsCalEv).not.toBeNull();
      expect(outlookCalEv).not.toBeNull();
      expect(clashCalEv).not.toBeNull();

      expect(getComputedStyle(googleCalEv.nativeElement).backgroundColor).not.toBe(
        getComputedStyle(teamsCalEv.nativeElement).backgroundColor
      );
      expect(getComputedStyle(outlookCalEv.nativeElement).backgroundColor).not.toBe(
        getComputedStyle(googleCalEv.nativeElement).backgroundColor
      );
      const clashStyle = getComputedStyle(clashCalEv.nativeElement);
      expect(clashStyle.borderTopStyle).toBe('dashed');
      expect(parseFloat(clashStyle.borderTopWidth)).toBeGreaterThanOrEqual(1);
      expect(clashStyle.borderTopColor).toBe('rgb(180, 35, 24)');

      // Filter Outlook in Calendario mode reduces .cal-ev count
      const outlookChip = blockEl.queryAll(By.css('.chips .chip')).find((c) => c.nativeElement.textContent.includes('Outlook'));
      outlookChip!.nativeElement.click();
      fixture.detectChanges();
      expect(blockEl.queryAll(By.css('.cal-ev')).length).toBe(1);

      // Output emissions
      const syncSpy = spyOn((component as any).agendaSyncRequested, 'emit');
      const noteSpy = spyOn((component as any).calendarNoteRequested, 'emit');
      blockEl.query(By.css('[data-agenda-sync]')).nativeElement.click();
      expect(syncSpy).toHaveBeenCalledWith({ blockId: 'blk-agenda' });

      // Back to Lista to click [data-event-note]
      segButtons[0].nativeElement.click();
      blockEl.queryAll(By.css('.chips .chip')).find((c) => c.nativeElement.textContent.includes('Todos'))!.nativeElement.click();
      fixture.detectChanges();
      blockEl.query(By.css('[data-event-note]')).nativeElement.click();
      expect(noteSpy).toHaveBeenCalled();
    });
  });
});
