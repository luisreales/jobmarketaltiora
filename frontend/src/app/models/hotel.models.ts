export interface Hotel {
  id: number;
  name: string;
  city: string;
  currentPrice: number | null;
  currentPriceSource: string;
}

export interface HotelPricePoint {
  price: number;
  currency: string;
  dateCaptured: string;
  source: string;
  isSeed: boolean;
  searchCity: string | null;
  checkIn: string | null;
  checkOut: string | null;
}

export interface HotelAnalysis {
  hotelId: number;
  hotelName: string;
  currentPrice: number;
  averagePrice: number;
  score: number;
  isDeal: boolean;
}

export interface BookingSearchResult {
  name: string;
  city: string;
  price: number;
  currency: string;
  offerUrl?: string | null;
  source: string;
  capturedAt: string;
  checkIn: string;
  checkOut: string;
  note?: string;
}

export interface SearchQuery {
  location: string;
  checkIn: string;
  checkOut: string;
  adults: number;
  kids: number;
  rooms: number;
  mode: 'real' | 'database';
}
