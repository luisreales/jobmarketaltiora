import { Component, EventEmitter, Output } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatNativeDateModule } from '@angular/material/core';
import { SearchQuery } from '../../models/hotel.models';

@Component({
  selector: 'app-search-form',
  imports: [
    ReactiveFormsModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatButtonToggleModule,
    MatDatepickerModule,
    MatNativeDateModule,
    MatIconModule
  ],
  templateUrl: './search-form.html',
  styleUrl: './search-form.scss'
})
export class SearchForm {
  @Output() searchSubmitted = new EventEmitter<SearchQuery>();

  form;

  constructor(private readonly fb: FormBuilder) {
    this.form = this.fb.group({
      location: ['', Validators.required],
      checkIn: [null as Date | null, Validators.required],
      checkOut: [null as Date | null, Validators.required],
      adults: [2, [Validators.required, Validators.min(1)]],
      kids: [1, [Validators.required, Validators.min(0)]],
      rooms: [1, [Validators.required, Validators.min(1)]],
      mode: ['real' as const, Validators.required]
    });
  }

  increase(controlName: 'adults' | 'kids' | 'rooms'): void {
    const current = Number(this.form.get(controlName)?.value ?? 0);
    this.form.get(controlName)?.setValue(current + 1);
  }

  decrease(controlName: 'adults' | 'kids' | 'rooms'): void {
    const current = Number(this.form.get(controlName)?.value ?? 0);
    const min = controlName === 'kids' ? 0 : 1;
    this.form.get(controlName)?.setValue(Math.max(min, current - 1));
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const raw = this.form.getRawValue();
    this.searchSubmitted.emit({
      location: raw.location ?? '',
      checkIn: this.toIsoDate(raw.checkIn),
      checkOut: this.toIsoDate(raw.checkOut),
      adults: Number(raw.adults ?? 2),
      kids: Number(raw.kids ?? 1),
      rooms: Number(raw.rooms ?? 1),
      mode: raw.mode ?? 'real'
    });
  }

  private toIsoDate(value: Date | string | null): string {
    if (!value) {
      return '';
    }

    if (typeof value === 'string') {
      return value;
    }

    const year = value.getFullYear();
    const month = String(value.getMonth() + 1).padStart(2, '0');
    const day = String(value.getDate()).padStart(2, '0');
    return `${year}-${month}-${day}`;
  }
}
