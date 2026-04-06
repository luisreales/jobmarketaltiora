import { Component, Input, OnChanges } from '@angular/core';
import { BaseChartDirective } from 'ng2-charts';
import {
  CategoryScale,
  Chart,
  ChartConfiguration,
  Filler,
  Legend,
  LineController,
  LineElement,
  LinearScale,
  PointElement,
  Tooltip
} from 'chart.js';
import { HotelPricePoint } from '../../models/hotel.models';

Chart.register(LineController, LineElement, PointElement, CategoryScale, LinearScale, Tooltip, Legend, Filler);

@Component({
  selector: 'app-hotel-chart',
  imports: [BaseChartDirective],
  templateUrl: './hotel-chart.html',
  styleUrl: './hotel-chart.scss'
})
export class HotelChart implements OnChanges {
  @Input() prices: HotelPricePoint[] = [];

  lineData: ChartConfiguration<'line'>['data'] = {
    labels: [],
    datasets: [
      {
        data: [],
        label: 'Price',
        borderColor: '#3f6c46',
        backgroundColor: 'rgba(64, 128, 69, 0.25)',
        tension: 0.25,
        fill: true
      }
    ]
  };

  lineOptions: ChartConfiguration<'line'>['options'] = {
    responsive: true,
    maintainAspectRatio: false
  };

  ngOnChanges(): void {
    this.lineData = {
      labels: this.prices.map((p) => new Date(p.dateCaptured).toLocaleDateString()),
      datasets: [
        {
          data: this.prices.map((p) => p.price),
          label: 'Price',
          borderColor: '#3f6c46',
          backgroundColor: 'rgba(64, 128, 69, 0.25)',
          tension: 0.25,
          fill: true
        }
      ]
    };
  }

}
