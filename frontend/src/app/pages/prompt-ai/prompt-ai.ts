import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AiPromptTemplate } from '../../models/ai-audit.models';
import { PromptAiService } from '../../services/prompt-ai.service';

type ViewMode = 'list' | 'create' | 'edit';

@Component({
  selector: 'app-prompt-ai',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './prompt-ai.html'
})
export class PromptAi implements OnInit {
  private readonly service = inject(PromptAiService);

  // List state
  templates: AiPromptTemplate[] = [];
  listLoading = false;

  // View mode
  viewMode: ViewMode = 'list';

  // Create form
  newKey = '';
  newTemplate = '';
  newVersion = 'v1';
  newIsActive = true;
  createLoading = false;
  createError = '';

  // Edit state
  editingKey = '';
  editTemplate = '';
  editVersion = 'v1';
  editIsActive = true;
  editLoading = false;
  editError = '';
  editSuccess = '';

  // Delete state
  deletingKeys = new Set<string>();
  deleteErrors = new Map<string, string>();

  ngOnInit(): void {
    this.loadAll();
  }

  loadAll(): void {
    this.listLoading = true;
    this.service.getAll().subscribe({
      next: (t) => { this.templates = t; this.listLoading = false; },
      error: () => { this.listLoading = false; }
    });
  }

  // ── Create ────────────────────────────────────────────────────────────────

  openCreate(): void {
    this.newKey = '';
    this.newTemplate = '';
    this.newVersion = 'v1';
    this.newIsActive = true;
    this.createError = '';
    this.viewMode = 'create';
  }

  submitCreate(): void {
    if (!this.newKey.trim() || !this.newTemplate.trim() || this.createLoading) return;
    this.createLoading = true;
    this.createError = '';

    this.service.create({
      key: this.newKey.trim().toLowerCase(),
      template: this.newTemplate.trim(),
      version: this.newVersion.trim() || 'v1',
      isActive: this.newIsActive,
      updatedBy: 'prompt-ai-ui'
    }).subscribe({
      next: () => {
        this.createLoading = false;
        this.viewMode = 'list';
        this.loadAll();
      },
      error: (err) => {
        this.createLoading = false;
        const msg = err?.error?.message;
        this.createError = msg ?? 'Failed to create prompt. The key may already exist.';
      }
    });
  }

  // ── Edit ──────────────────────────────────────────────────────────────────

  openEdit(t: AiPromptTemplate): void {
    this.editingKey = t.key;
    this.editTemplate = t.template;
    this.editVersion = t.version;
    this.editIsActive = t.isActive;
    this.editError = '';
    this.editSuccess = '';
    this.viewMode = 'edit';
  }

  submitEdit(): void {
    if (!this.editTemplate.trim() || this.editLoading) return;
    this.editLoading = true;
    this.editError = '';
    this.editSuccess = '';

    this.service.update(this.editingKey, {
      template: this.editTemplate.trim(),
      version: this.editVersion.trim() || 'v1',
      isActive: this.editIsActive,
      updatedBy: 'prompt-ai-ui'
    }).subscribe({
      next: (updated) => {
        this.editLoading = false;
        this.editSuccess = 'Prompt saved successfully.';
        const idx = this.templates.findIndex(t => t.key === this.editingKey);
        if (idx >= 0) this.templates[idx] = updated;
      },
      error: () => {
        this.editLoading = false;
        this.editError = 'Failed to save. Please try again.';
      }
    });
  }

  // ── Delete ────────────────────────────────────────────────────────────────

  deletePrompt(key: string): void {
    if (this.deletingKeys.has(key)) return;
    this.deletingKeys.add(key);
    this.deleteErrors.delete(key);

    this.service.delete(key).subscribe({
      next: () => {
        this.deletingKeys.delete(key);
        this.templates = this.templates.filter(t => t.key !== key);
        if (this.viewMode === 'edit' && this.editingKey === key) {
          this.viewMode = 'list';
        }
      },
      error: () => {
        this.deletingKeys.delete(key);
        this.deleteErrors.set(key, 'Delete failed.');
      }
    });
  }

  // ── Helpers ───────────────────────────────────────────────────────────────

  goBack(): void {
    this.viewMode = 'list';
  }

  activeClass(isActive: boolean): string {
    return isActive
      ? 'bg-green-100 text-green-700'
      : 'bg-slate-100 text-slate-500';
  }
}
