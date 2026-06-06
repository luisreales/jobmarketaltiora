import { Component } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  templateUrl: './app.html',
  styleUrl: './app.scss'
})
export class App {
  isSidebarOpen = false;
  openSections = new Set<string>(['Intelligence', 'Strategy', 'Tools']);

  readonly navSections = [
    {
      items: [
        { path: '/jobs', label: 'Jobs' },
        { path: '/opportunities', label: 'Opportunities' },
        { path: '/products', label: 'Products' },
      ]
    },
    {
      label: 'Intelligence',
      items: [
        { path: '/revenue', label: '💰 Revenue' },
        { path: '/companies', label: '🏢 Prospects' },
        { path: '/opportunity-ideas', label: '💡 Idea Vault' },
        { path: '/intelligence', label: '📊 Intelligence' },
        { path: '/clusters', label: 'Clusters' },
        { path: '/semantic-groups', label: 'Semantic Groups' },
        { path: '/technologies', label: '🔬 Technologies' },
        { path: '/trends', label: '📈 Trends' },
        { path: '/stack-graph', label: '🕸 Stack Graph' },
      ]
    },
    {
      label: 'Strategy',
      items: [
        { path: '/commercial-strategies', label: '📊 Commercial Strategy' },
        { path: '/mvp-requirements', label: '🛠 MVP Requirements' },
      ]
    },
    {
      label: 'Tools',
      items: [
        { path: '/ai-audit', label: 'AI Audit' },
        { path: '/prompt-ai', label: 'Prompt AI' },
        { path: '/scraping', label: 'Scraping' },
        { path: '/appsumo', label: '🌮 AppSumo' },
      ]
    }
  ];

  toggleSidebar(): void {
    this.isSidebarOpen = !this.isSidebarOpen;
  }

  closeSidebar(): void {
    this.isSidebarOpen = false;
  }

  toggleSection(label: string): void {
    if (this.openSections.has(label)) {
      this.openSections.delete(label);
    } else {
      this.openSections.add(label);
    }
  }
}
