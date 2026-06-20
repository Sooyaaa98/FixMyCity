// src/app/shared/components/chatbot-widget/chatbot-widget.component.ts

import { Component, OnInit, OnDestroy, ViewChild, ElementRef, AfterViewChecked } from '@angular/core';
import { FormControl, Validators } from '@angular/forms';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';
import { MlService } from '../../../fmc-services/ml.service';
import { IChatMessage } from '../../../fmc-interfaces/ml.interface';

@Component({
  selector: 'app-chatbot-widget',
  templateUrl: './chatbot-widget.component.html',
  styleUrls: ['./chatbot-widget.component.css']
})
export class ChatbotWidgetComponent implements OnInit, OnDestroy, AfterViewChecked {

  @ViewChild('messagesContainer') messagesRef!: ElementRef<HTMLDivElement>;

  isOpen   = false;
  isTyping = false;

  messages: IChatMessage[] = [];
  inputCtrl = new FormControl('', [Validators.required, Validators.maxLength(500)]);

  /**
   * Tracks how many messages the user has "seen" (i.e. were present the
   * last time the panel was open or the chat was cleared).  The badge only
   * pulses when new bot messages have arrived *since* the panel was last open.
   * Starts at 1 because the greeting is visible on first open.
   */
  private lastSeenCount = 1;

  private readonly GREETING: IChatMessage = {
    role: 'assistant',
    content: 'Hi! I\'m the FixMyCity assistant. Ask me about complaints, status updates, or how the platform works.'
  };

  constructor(
    private ml: MlService,
    private sanitizer: DomSanitizer
  ) {}

  ngOnInit(): void {
    this.messages = [{ ...this.GREETING }];
  }

  ngOnDestroy(): void {}

  ngAfterViewChecked(): void {
    this.scrollToBottom();
  }

  toggle(): void {
    this.isOpen = !this.isOpen;
    // Mark all messages seen whenever the panel becomes visible.
    if (this.isOpen) this.lastSeenCount = this.messages.length;
  }

  close(): void {
    this.isOpen = false;
    // User had everything in view while open — mark all as read on close too.
    this.lastSeenCount = this.messages.length;
  }

  send(): void {
    const text = (this.inputCtrl.value ?? '').trim();
    if (!text || this.isTyping) return;

    const userMsg: IChatMessage = { role: 'user', content: text };
    this.messages = [...this.messages, userMsg];
    this.inputCtrl.reset();
    this.isTyping = true;

    this.ml.chat({ messages: this.messages }).subscribe({
      next: (res) => {
        this.messages = [...this.messages, { role: 'assistant', content: res.reply }];
        this.isTyping = false;
      },
      error: () => {
        this.messages = [...this.messages,
          { role: 'assistant', content: 'Sorry, I\'m temporarily unavailable. Please try again.' }];
        this.isTyping = false;
      }
    });
  }

  onKeydown(event: KeyboardEvent): void {
    if (event.key === 'Enter' && !event.shiftKey) {
      event.preventDefault();
      this.send();
    }
  }

  clearChat(): void {
    this.messages = [{ ...this.GREETING }];
    this.lastSeenCount = 1; // greeting is visible immediately after clear
  }

  /**
   * Returns true ONLY when the chat is closed AND the bot has sent new
   * messages the user hasn't seen yet (i.e. since lastSeenCount).
   * This prevents the FAB from buzzing permanently after any conversation.
   */
  get unreadBadge(): boolean {
    return !this.isOpen && this.messages.length > this.lastSeenCount;
  }

  /**
   * Converts the bot's plain-text reply (with minimal markdown) to safe HTML.
   *
   * Supported patterns Gemini is prompted to emit:
   *   **bold text**          → <strong>bold text</strong>
   *   - bullet item          → <li>bullet item</li> (grouped in <ul>)
   *   \n (newline)           → line break / paragraph separation
   *
   * Only applied to assistant messages; user bubbles are always plain text.
   */
  formatBotMessage(text: string): SafeHtml {
    if (!text) return this.sanitizer.bypassSecurityTrustHtml('');

    // 1. Escape HTML entities to prevent injection
    let escaped = text
      .replace(/&/g, '&amp;')
      .replace(/</g, '&lt;')
      .replace(/>/g, '&gt;');

    // 2. Bold: **text** → <strong>text</strong>
    escaped = escaped.replace(/\*\*(.+?)\*\*/g, '<strong>$1</strong>');

    // 3. Bullet lists: lines starting with "- " or "• "
    //    Collect consecutive bullet lines into a <ul>
    const lines = escaped.split('\n');
    const processed: string[] = [];
    let inList = false;

    for (const line of lines) {
      const isBullet = /^[-•]\s+/.test(line.trim());

      if (isBullet) {
        if (!inList) { processed.push('<ul class="chat-list">'); inList = true; }
        processed.push(`<li>${line.trim().replace(/^[-•]\s+/, '')}</li>`);
      } else {
        if (inList) { processed.push('</ul>'); inList = false; }
        if (line.trim() === '') {
          processed.push('<br>');
        } else {
          processed.push(`<span class="chat-line">${line}</span>`);
        }
      }
    }
    if (inList) processed.push('</ul>');

    const html = processed.join('\n');
    return this.sanitizer.bypassSecurityTrustHtml(html);
  }

  private scrollToBottom(): void {
    try {
      if (this.messagesRef) {
        const el = this.messagesRef.nativeElement;
        el.scrollTop = el.scrollHeight;
      }
    } catch {}
  }
}
