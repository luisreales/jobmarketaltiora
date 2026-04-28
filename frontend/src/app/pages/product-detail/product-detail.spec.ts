import { ComponentFixture, TestBed, fakeAsync, tick } from '@angular/core/testing';
import { ActivatedRoute } from '@angular/router';
import { of, throwError } from 'rxjs';
import { ProductDetail } from './product-detail';
import { ProductService } from '../../services/product.service';
import { ProductSuggestion } from '../../models/market.models';

const BASE_PRODUCT: ProductSuggestion = {
  id: 1,
  productName: 'Deployment Automation Suite',
  productDescription: 'CI/CD pipeline accelerator',
  whyNow: 'Companies hiring DevOps need faster pipelines',
  offer: 'Fixed-price sprint',
  actionToday: 'Send cold email to CTO',
  techFocus: 'Azure+Kubernetes',
  estimatedBuildDays: 14,
  minDealSizeUsd: 5000,
  maxDealSizeUsd: 15000,
  totalJobCount: 5,
  avgDirectClientRatio: 0.8,
  avgUrgencyScore: 7,
  topBlueOceanScore: 6,
  clusterCount: 2,
  priorityScore: 90,
  opportunityType: 'Manual',
  industry: 'Technology',
  synthesisDetailJson: null,
  llmStatus: 'pending',
  generatedAt: new Date().toISOString()
};

const STRATEGY_JSON = JSON.stringify({
  realBusinessProblem: 'Manual deploys cost 40h/month.',
  financialImpact: 'Save $80k/year in engineering time.',
  mvpDefinition: 'CI/CD pipeline in 2 weeks.',
  targetBuyer: 'CTO — ROI from day 1.',
  pricingStrategy: 'Fixed $8k sprint.',
  outreachMessage: 'Hi, we can automate your deploys...'
});

const PRODUCT_WITH_STRATEGY: ProductSuggestion = {
  ...BASE_PRODUCT,
  synthesisDetailJson: STRATEGY_JSON,
  llmStatus: 'completed'
};

describe('ProductDetail', () => {
  let component: ProductDetail;
  let fixture: ComponentFixture<ProductDetail>;
  let productService: jasmine.SpyObj<ProductService>;

  beforeEach(async () => {
    productService = jasmine.createSpyObj('ProductService',
      ['getProduct', 'synthesize', 'synthesizeStrategy']);

    productService.getProduct.and.returnValue(of(BASE_PRODUCT));

    await TestBed.configureTestingModule({
      imports: [ProductDetail],
      providers: [
        { provide: ProductService,  useValue: productService },
        { provide: ActivatedRoute,  useValue: { snapshot: { paramMap: { get: (_: string) => '1' } } } }
      ]
    }).compileComponents();

    fixture   = TestBed.createComponent(ProductDetail);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  // ── Initialization ───────────────────────────────────────────────────────

  it('loads product on init', () => {
    expect(productService.getProduct).toHaveBeenCalledWith(1);
    expect(component.product?.productName).toBe('Deployment Automation Suite');
  });

  // ── commercialStrategy getter ────────────────────────────────────────────

  it('commercialStrategy returns null when synthesisDetailJson is null', () => {
    component.product = BASE_PRODUCT;
    expect(component.commercialStrategy).toBeNull();
  });

  it('commercialStrategy returns null for tactical plan json (no realBusinessProblem)', () => {
    component.product = {
      ...BASE_PRODUCT,
      synthesisDetailJson: JSON.stringify({
        implementacion: 'Step 1...', requerimientos: 'Node 18',
        tiempo_y_tecnologias: '2 weeks', empresas_objetivo: 'Acme'
      })
    };
    expect(component.commercialStrategy).toBeNull();
  });

  it('commercialStrategy parses and returns strategy object', () => {
    component.product = PRODUCT_WITH_STRATEGY;
    const strategy = component.commercialStrategy;
    expect(strategy).not.toBeNull();
    expect(strategy?.realBusinessProblem).toContain('Manual deploys');
    expect(strategy?.outreachMessage).toContain('automate');
  });

  it('commercialStrategy returns null on malformed json', () => {
    component.product = { ...BASE_PRODUCT, synthesisDetailJson: '{bad json' };
    expect(component.commercialStrategy).toBeNull();
  });

  // ── dealRange helper ─────────────────────────────────────────────────────

  it('dealRange formats values below 1000 as dollar amounts', () => {
    component.product = { ...BASE_PRODUCT, minDealSizeUsd: 500, maxDealSizeUsd: 999 };
    expect(component.dealRange()).toBe('$500 – $999');
  });

  it('dealRange formats values >= 1000 in K notation', () => {
    component.product = { ...BASE_PRODUCT, minDealSizeUsd: 5000, maxDealSizeUsd: 15000 };
    expect(component.dealRange()).toBe('$5K – $15K');
  });

  // ── generateStrategy ────────────────────────────────────────────────────

  it('generateStrategy calls synthesizeStrategy and reloads product on success', fakeAsync(() => {
    productService.synthesizeStrategy.and.returnValue(of(PRODUCT_WITH_STRATEGY));
    productService.getProduct.and.returnValue(of(PRODUCT_WITH_STRATEGY));

    component.generateStrategy();
    tick();

    expect(productService.synthesizeStrategy).toHaveBeenCalledWith(1);
    expect(productService.getProduct).toHaveBeenCalledTimes(2); // init + after strategy
    expect(component.strategyLoading).toBeFalse();
  }));

  it('generateStrategy sets strategyError on failure', fakeAsync(() => {
    productService.synthesizeStrategy.and.returnValue(throwError(() => new Error('AI error')));

    component.generateStrategy();
    tick();

    expect(component.strategyError).toBeTruthy();
    expect(component.strategyLoading).toBeFalse();
  }));

  it('generateStrategy is a no-op if already loading', () => {
    component.strategyLoading = true;
    component.generateStrategy();
    expect(productService.synthesizeStrategy).not.toHaveBeenCalled();
  });

  // ── opportunityTypeClass helper ──────────────────────────────────────────

  it('opportunityTypeClass returns purple for MVPProduct', () => {
    expect(component.opportunityTypeClass('MVPProduct')).toContain('purple');
  });

  it('opportunityTypeClass returns green for QuickWin', () => {
    expect(component.opportunityTypeClass('QuickWin')).toContain('green');
  });

  it('opportunityTypeClass returns amber for Consulting', () => {
    expect(component.opportunityTypeClass('Consulting')).toContain('amber');
  });

  it('opportunityTypeClass returns slate for unknown type', () => {
    expect(component.opportunityTypeClass('Manual')).toContain('slate');
  });

  // ── techTokens helper ────────────────────────────────────────────────────

  it('techTokens splits by + and trims whitespace', () => {
    expect(component.techTokens('Azure + Kubernetes + Terraform')).toEqual(['Azure', 'Kubernetes', 'Terraform']);
  });
});
