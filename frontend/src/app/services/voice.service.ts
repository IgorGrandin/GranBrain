import { Injectable, NgZone } from '@angular/core';
import { Observable, Subject } from 'rxjs';

@Injectable({
  providedIn: 'root'
})
export class VoiceService {
  private recognition: any = null;
  private transcriptSubject = new Subject<string>();
  private listeningSubject = new Subject<boolean>();
  private isCurrentlyListening = false;

  constructor(private zone: NgZone) {
    this.initSpeechRecognition();
  }

  public get isSupported(): boolean {
    return typeof window !== 'undefined' &&
      (('SpeechRecognition' in window) || ('webkitSpeechRecognition' in window));
  }

  public get isListening$(): Observable<boolean> {
    return this.listeningSubject.asObservable();
  }

  public get onTranscript$(): Observable<string> {
    return this.transcriptSubject.asObservable();
  }

  private initSpeechRecognition(): void {
    if (!this.isSupported) {
      return;
    }

    const SpeechRecognition = (window as any).SpeechRecognition || (window as any).webkitSpeechRecognition;
    this.recognition = new SpeechRecognition();
    this.recognition.continuous = false;
    this.recognition.interimResults = false;
    this.recognition.lang = 'pt-BR';

    this.recognition.onstart = () => {
      this.zone.run(() => {
        this.isCurrentlyListening = true;
        this.listeningSubject.next(true);
      });
    };

    this.recognition.onresult = (event: any) => {
      this.zone.run(() => {
        if (event.results && event.results.length > 0) {
          const transcript = event.results[0][0]?.transcript || '';
          if (transcript) {
            this.transcriptSubject.next(transcript.trim());
          }
        }
      });
    };

    this.recognition.onerror = (err: any) => {
      this.zone.run(() => {
        this.isCurrentlyListening = false;
        this.listeningSubject.next(false);
      });
    };

    this.recognition.onend = () => {
      this.zone.run(() => {
        this.isCurrentlyListening = false;
        this.listeningSubject.next(false);
      });
    };
  }

  public startListening(): boolean {
    if (!this.recognition) {
      return false;
    }
    try {
      this.recognition.start();
      return true;
    } catch {
      return false;
    }
  }

  public stopListening(): void {
    if (!this.recognition) return;
    try {
      this.recognition.stop();
    } catch {
      // Ignore
    }
    this.isCurrentlyListening = false;
    this.listeningSubject.next(false);
  }

  public speak(text: string, lang = 'pt-BR'): void {
    if (typeof window === 'undefined' || !('speechSynthesis' in window)) {
      return;
    }
    try {
      window.speechSynthesis.cancel();
      const utterance = new SpeechSynthesisUtterance(text);
      utterance.lang = lang;
      utterance.rate = 1.0;
      window.speechSynthesis.speak(utterance);
    } catch {
      // Ignore
    }
  }
}
