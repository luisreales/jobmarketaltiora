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
    { path: '/products', label: 'Products' },
    { path: '/ai-audit', label: 'AI Audit' },
    { path: '/scraping', label: 'Scraping' }
  ];

  toggleSidebar(): void {
    this.isSidebarOpen = !this.isSidebarOpen;
  }

  closeSidebar(): void {
    this.isSidebarOpen = false;
  }
}
