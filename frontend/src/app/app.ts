import { Component } from '@angular/core';
import { NgFor } from '@angular/common';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';

@Component({
  selector: 'app-root',
  imports: [NgFor, RouterOutlet, RouterLink, RouterLinkActive],
  templateUrl: './app.html',
  styleUrl: './app.scss'
})
export class App {
  isSidebarOpen = false;

  readonly navigationItems = [
    { path: '/jobs', label: 'Jobs' },
    { path: '/opportunities', label: 'Opportunities' },
    { path: '/opportunity-ideas', label: '💡 Idea Vault' },
    { path: '/products', label: 'Products' },
    { path: '/commercial-strategies', label: '📊 Commercial Strategy' },
    { path: '/mvp-requirements', label: '🛠 MVP Requirements' },
    { path: '/ai-audit', label: 'AI Audit' },
    { path: '/prompt-ai', label: 'Prompt AI' },
    { path: '/scraping', label: 'Scraping' }
  ];

  toggleSidebar(): void {
    this.isSidebarOpen = !this.isSidebarOpen;
  }

  closeSidebar(): void {
    this.isSidebarOpen = false;
  }
}
