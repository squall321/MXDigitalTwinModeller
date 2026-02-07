# MX Digital Twin Modeller - Implementation Plan
## 물성 측정 시험 시편 자동 생성 시스템

---

## 1. 프로젝트 개요

### 목표
표준 물성 측정 시험에 사용되는 다양한 시편을 자동으로 모델링하는 SpaceClaim AddIn 개발

### 지원 시험 종류
1. **인장시험 (Tensile Test)** ✓ 완료
2. **DMA 인장시편 (DMA Tensile)**
3. **DMA 3점 굽힘시편 (DMA 3-Point Bending)**
4. **DMA 4점 굽힘시편 (DMA 4-Point Bending)**

---

## 2. 시편 규격 및 치수

### 2.1 인장시험 시편 (완료)
- **ASTM E8** (금속)
  - Standard: GL 50mm, GW 12.5mm, Total 200mm
  - SubSize: GL 25mm, GW 6mm, Total 100mm
- **ASTM D638** (플라스틱)
  - Type I: GL 50mm, GW 13mm, Total 165mm
  - Type II: GL 57mm, GW 6mm, Total 183mm

### 2.2 DMA 인장시편
**표준 규격: ASTM D4065, ISO 6721**

| 파라미터 | 표준값 | 범위 |
|---------|--------|------|
| 길이 (Length) | 50 mm | 30-100 mm |
| 폭 (Width) | 10 mm | 5-15 mm |
| 두께 (Thickness) | 3 mm | 1-5 mm |
| 게이지 길이 (Gauge Length) | 20 mm | 10-40 mm |
| 그립 길이 (Grip Length) | 10 mm | 5-20 mm |

**형상**: 직사각형 또는 dog-bone 형상

### 2.3 DMA 3점 굽힘시편
**표준 규격: ASTM D790, ISO 178**

| 파라미터 | 표준값 | 범위 |
|---------|--------|------|
| 길이 (Length) | 80 mm | 50-150 mm |
| 폭 (Width) | 10 mm | 5-25 mm |
| 두께 (Thickness) | 4 mm | 1-10 mm |
| 지지점 간격 (Span) | 64 mm | 16×Thickness |
| 스팬/두께 비율 | 16:1 | 16:1 ~ 32:1 |

**형상**: 직사각형 바 (Rectangular Bar)

**지지 구조**:
- 하부 지지점 2개 (Support Points)
- 상부 로딩 노즈 1개 (Loading Nose)

### 2.4 DMA 4점 굽힘시편
**표준 규격: ASTM C1161, ASTM D6272**

| 파라미터 | 표준값 | 범위 |
|---------|--------|------|
| 길이 (Length) | 100 mm | 60-200 mm |
| 폭 (Width) | 10 mm | 5-25 mm |
| 두께 (Thickness) | 4 mm | 1-10 mm |
| 외부 스팬 (Outer Span) | 80 mm | 60-160 mm |
| 내부 스팬 (Inner Span) | 40 mm | 20-80 mm |
| 스팬 비율 | 2:1 | 2:1 ~ 3:1 |

**형상**: 직사각형 바 (Rectangular Bar)

**지지 구조**:
- 하부 지지점 2개 (Outer Support Points)
- 상부 로딩 노즈 2개 (Inner Loading Noses)

---

## 3. 클래스 구조 설계

### 3.1 모델 (Models)

```
Models/
├── TensileTest/              [완료]
│   ├── ASTMSpecimenType.cs
│   └── TensileSpecimenParameters.cs
│
└── DMA/                      [신규]
    ├── DMASpecimenType.cs
    ├── DMATestType.cs
    ├── DMATensileParameters.cs
    ├── DMA3PointBendingParameters.cs
    └── DMA4PointBendingParameters.cs
```

### 3.2 서비스 (Services)

```
Services/
├── TensileTest/              [완료]
│   ├── ASTMSpecimenFactory.cs
│   └── SpecimenModelingService.cs
│
└── DMA/                      [신규]
    ├── DMASpecimenFactory.cs
    ├── DMATensileService.cs
    ├── DMA3PointBendingService.cs
    └── DMA4PointBendingService.cs
```

### 3.3 커맨드 (Commands)

```
Commands/
├── TensileTest/              [완료]
│   └── CreateASTMTensileSpecimenCommand.cs
│
└── DMA/                      [신규]
    ├── CreateDMATensileSpecimenCommand.cs
    ├── CreateDMA3PointBendingCommand.cs
    └── CreateDMA4PointBendingCommand.cs
```

### 3.4 UI (Dialogs)

```
UI/Dialogs/
├── TensileSpecimenDialog.cs  [완료]
│
└── DMA/                       [신규]
    ├── DMASpecimenDialog.cs
    ├── DMATensilePanel.cs
    ├── DMA3PointBendingPanel.cs
    └── DMA4PointBendingPanel.cs
```

---

## 4. 리본 UI 구조

```
[MX Modeller] 탭
│
├── [Tensile Test] 그룹
│   └── Create ASTM Tensile Specimen
│
└── [DMA Test] 그룹 (신규)
    ├── Create DMA Tensile Specimen
    ├── Create DMA 3-Point Bending
    └── Create DMA 4-Point Bending
```

---

## 5. 구현 순서

### Phase 1: DMA 기본 구조 (1단계)
- [ ] DMASpecimenType 열거형
- [ ] DMATestType 열거형
- [ ] DMASpecimenFactory 기본 클래스
- [ ] Ribbon에 DMA Test 그룹 추가

### Phase 2: DMA 인장시편 (2단계)
- [ ] DMATensileParameters 모델
- [ ] DMATensileService 서비스
- [ ] CreateDMATensileSpecimenCommand 커맨드
- [ ] DMATensilePanel UI
- [ ] 시편 + 그립 장비 모델링
- [ ] 테스트 및 검증

### Phase 3: DMA 3점 굽힘시편 (3단계)
- [ ] DMA3PointBendingParameters 모델
- [ ] DMA3PointBendingService 서비스
- [ ] CreateDMA3PointBendingCommand 커맨드
- [ ] DMA3PointBendingPanel UI
- [ ] 시편 + 지지구조 모델링
  - 하부 지지점 2개
  - 상부 로딩 노즈 1개
- [ ] 테스트 및 검증

### Phase 4: DMA 4점 굽힘시편 (4단계)
- [ ] DMA4PointBendingParameters 모델
- [ ] DMA4PointBendingService 서비스
- [ ] CreateDMA4PointBendingCommand 커맨드
- [ ] DMA4PointBendingPanel UI
- [ ] 시편 + 지지구조 모델링
  - 하부 지지점 2개 (외부)
  - 상부 로딩 노즈 2개 (내부)
- [ ] 테스트 및 검증

### Phase 5: 통합 및 최적화 (5단계)
- [ ] 공통 코드 리팩토링
- [ ] 파라미터 검증 강화
- [ ] 에러 처리 개선
- [ ] 문서화
- [ ] 사용자 가이드 작성

---

## 6. 데이터 모델 상세

### 6.1 DMASpecimenType
```csharp
public enum DMASpecimenType
{
    Standard,           // 표준 사이즈
    Custom             // 사용자 정의
}
```

### 6.2 DMATestType
```csharp
public enum DMATestType
{
    Tensile,           // 인장
    ThreePointBending, // 3점 굽힘
    FourPointBending   // 4점 굽힘
}
```

### 6.3 DMATensileParameters
```csharp
- Length: double
- Width: double
- Thickness: double
- GaugeLength: double
- GripLength: double
- Shape: SpecimenShape (Rectangle/DogBone)
```

### 6.4 DMA3PointBendingParameters
```csharp
- Length: double
- Width: double
- Thickness: double
- Span: double
- SupportDiameter: double
- LoadingNoseDiameter: double
```

### 6.5 DMA4PointBendingParameters
```csharp
- Length: double
- Width: double
- Thickness: double
- OuterSpan: double
- InnerSpan: double
- SupportDiameter: double
- LoadingNoseDiameter: double
```

---

## 7. 생성되는 파트 구조

### 7.1 DMA 인장시편
1. 시편 본체 (1개)
2. 상부 그립 (2개)
3. 하부 그립 (2개)

### 7.2 DMA 3점 굽힘
1. 시편 본체 (1개)
2. 하부 지지점 좌 (1개)
3. 하부 지지점 우 (1개)
4. 상부 로딩 노즈 (1개)

### 7.3 DMA 4점 굽힘
1. 시편 본체 (1개)
2. 하부 외부 지지점 좌 (1개)
3. 하부 외부 지지점 우 (1개)
4. 상부 내부 로딩 노즈 좌 (1개)
5. 상부 내부 로딩 노즈 우 (1개)

---

## 8. 기술적 고려사항

### 8.1 공통 유틸리티
- 원통형 지지점/로딩 노즈 생성 함수
- 스팬 계산 및 검증 함수
- 표준 규격 검증 함수

### 8.2 파라미터 검증
- 스팬/두께 비율 검증
- 최소/최대 치수 검증
- 지지점 간격 검증

### 8.3 모델링 정확도
- 접촉면 정확도 고려
- 실제 시험 조건 반영
- FEM 해석 호환성

---

## 9. 테스트 계획

### 9.1 단위 테스트
- 파라미터 검증 테스트
- 치수 계산 테스트
- 형상 생성 테스트

### 9.2 통합 테스트
- 전체 시편 생성 테스트
- 여러 규격 조합 테스트
- 에러 처리 테스트

### 9.3 사용자 테스트
- UI 직관성 테스트
- 실제 사용 시나리오 테스트
- 성능 테스트

---

## 10. 일정

| Phase | 작업 내용 | 예상 시간 |
|-------|----------|----------|
| Phase 1 | DMA 기본 구조 | 1일 |
| Phase 2 | DMA 인장시편 | 2일 |
| Phase 3 | DMA 3점 굽힘 | 2일 |
| Phase 4 | DMA 4점 굽힘 | 2일 |
| Phase 5 | 통합 및 최적화 | 1일 |
| **총계** | | **8일** |

---

## 11. 참고 자료

### 표준 규격
- ASTM D4065: Standard Practice for Plastics - Dynamic Mechanical Properties
- ASTM D790: Flexural Properties of Unreinforced and Reinforced Plastics
- ASTM D6272: Flexural Properties by Four-Point Bending
- ASTM C1161: Flexural Strength of Advanced Ceramics
- ISO 6721: Plastics - Determination of Dynamic Mechanical Properties
- ISO 178: Plastics - Determination of Flexural Properties

### DMA 시험 원리
- 동적 기계 분석 (Dynamic Mechanical Analysis)
- 점탄성 특성 측정
- 저장 탄성률 (Storage Modulus)
- 손실 탄성률 (Loss Modulus)
- 손실 계수 (Tan Delta)

---

## 12. 다음 단계

**현재 작업**: Phase 1 - DMA 기본 구조 구현

**진행 방법**:
1. ✅ 계획 문서 작성 완료
2. ⏭️ Phase 1 시작: DMA 기본 구조 만들기
   - DMASpecimenType.cs 생성
   - DMATestType.cs 생성
   - 폴더 구조 생성

사용자 확인 후 Phase 1부터 시작하겠습니다.
