using System.Reflection.PortableExecutable;
using AutoMapper;
using Mango.Services.ShoppingCartAPI.Data;
using Mango.Services.ShoppingCartAPI.Models;
using Mango.Services.ShoppingCartAPI.Models.Dto;
using Mango.Services.ShoppingCartAPI.Service.IService;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Mango.Services.ShoppingCartAPI.Controllers
{
    [Route("api/cart")]
    [ApiController]
    public class CartAPIController : ControllerBase
    {
        private ResponseDto responseDto;
        private IMapper _mapper;
        private readonly AppDbContext _appDbContext;
        private readonly IProductService _productService;
        private readonly ICouponService _couponService;

        public CartAPIController(AppDbContext appDbContext,IMapper mapper,IProductService productService,
            ICouponService couponService)
        {
            _appDbContext = appDbContext;
            _mapper = mapper;
            this.responseDto = new ResponseDto();
            _productService = productService;
            _couponService = couponService;
        }
        [HttpGet("GetCart/{userId}")]
        public async Task<ResponseDto> GetCart(string userId)
        {
            try
            {
                CartDto cart = new()
                {
                    CartHeader = _mapper.Map<CartHeaderDto>(_appDbContext.CartHeaders.First(u => u.UserId == userId))
                };

                cart.CartDetails=_mapper.Map<IEnumerable<CartDetailsDto>>(_appDbContext.CartDetails
                    .Where(u=>u.CartHeaderId==cart.CartHeader.CartHeaderId).ToList());

                IEnumerable<ProductDto> listProducts =await _productService.GetProducts();

                foreach(var item in cart.CartDetails)
                {
                    item.Product = listProducts.FirstOrDefault(u => u.ProductId == item.ProductId);
                    cart.CartHeader.CartTotal += (item.Count * item.Product.Price);
                }
                // apply coupon if any
                if (!string.IsNullOrEmpty(cart.CartHeader.CouponCode))
                {
                    CouponDto couponDto=await _couponService.GetCoupon(cart.CartHeader.CouponCode); 
                    if(couponDto != null && cart.CartHeader.CartTotal>couponDto.MinAmount)
                    {
                        cart.CartHeader.CartTotal = couponDto.DiscountAmount;
                        cart.CartHeader.Discount=couponDto.DiscountAmount;
                    }
                }
                responseDto.Result = cart;
            }
            catch(Exception ex)
            {
                responseDto.IsSuccess = false;
                responseDto.Message = ex.Message.ToString();
            }
            return responseDto;
        }

        [HttpPost("CartUpsert")]
        public async Task<ResponseDto> CartUpsert(CartDto cartDto)
        {
            try
            {
                var cartHeaderFromDb = await _appDbContext.CartHeaders.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == cartDto.CartHeader.UserId);
                if (cartHeaderFromDb == null)
                {
                    //create cart header and details
                    CartHeader cartHeader = _mapper.Map<CartHeader>(cartDto.CartHeader);
                    _appDbContext.Add(cartHeader);
                    await _appDbContext.SaveChangesAsync();
                    cartDto.CartDetails.First().CartHeaderId = cartHeader.CartHeaderId;
                    _appDbContext.CartDetails.Add(_mapper.Map<CartDetails>(cartDto.CartDetails.First()));
                    await _appDbContext.SaveChangesAsync();
                }
                else
                {
                    //if header is not null
                    //check if details has same product
                    var cartDetailsFromDb=await _appDbContext.CartDetails.AsNoTracking().FirstOrDefaultAsync(
                        u=>u.ProductId==cartDto.CartDetails.First().ProductId &&
                        u.CartHeaderId==cartHeaderFromDb.CartHeaderId);
                    if(cartDetailsFromDb == null)
                    {
                        //create cart details
                        cartDto.CartDetails.First().CartHeaderId = cartHeaderFromDb.CartHeaderId;
                        _appDbContext.CartDetails.Add(_mapper.Map<CartDetails>(cartDto.CartDetails.First()));
                        await _appDbContext.SaveChangesAsync();
                    }
                    else
                    {
                        //update count in the cart details
                        cartDto.CartDetails.First().Count += cartDetailsFromDb.Count;
                        cartDto.CartDetails.First().CartHeaderId = cartDetailsFromDb.CartHeaderId;
                        cartDto.CartDetails.First().CartDetailId = cartDetailsFromDb.CartDetailId;
                        _appDbContext.CartDetails.Update(_mapper.Map<CartDetails>(cartDto.CartDetails.First()));
                        await _appDbContext.SaveChangesAsync();
                    }
                    
                }
                responseDto.Result=cartDto;
            }
            catch (Exception ex)
            {
                responseDto.Message = ex.Message.ToString();
                responseDto.IsSuccess = false;
            }
            return responseDto;
        }

        [HttpPost("RemoveCart")]
        public async Task<ResponseDto> RemoveCart([FromBody] int cartDetailId)
        {
            try
            {
                CartDetails cartDetails = _appDbContext.CartDetails.First(u => u.CartDetailId == cartDetailId);

                int totalCountOfCartItem=_appDbContext.CartDetails.Where(u=>u.CartHeaderId==cartDetails.CartHeaderId).Count();
               _appDbContext.CartDetails.Remove(cartDetails);
                if (totalCountOfCartItem == 1)
                {
                    var cardHeaderRemove = await _appDbContext.CartHeaders.FirstOrDefaultAsync(u => u.CartHeaderId == cartDetails.CartHeaderId);
                    _appDbContext.CartHeaders.Remove(cardHeaderRemove);
                }
               await _appDbContext.SaveChangesAsync();
                responseDto.Result = true;
            }
            catch (Exception ex)
            {
                responseDto.Message = ex.Message.ToString();
                responseDto.IsSuccess = false;
            }
            return responseDto;
        }

        [HttpPost("ApplyCoupon")]
        public async Task<object> ApplyCoupon([FromBody] CartDto cartDto)
        {
            try
            {
                var cartFromDb=await _appDbContext.CartHeaders.FirstAsync(u=>u.UserId==cartDto.CartHeader.UserId);
                cartFromDb.CouponCode=cartDto.CartHeader.CouponCode;
                _appDbContext.CartHeaders.Update(cartFromDb);
                return await _appDbContext.SaveChangesAsync();
                responseDto.Result= true;
            }
            catch(Exception ex)
            {
                responseDto.IsSuccess=false;
                responseDto.Message = ex.Message.ToString();
            }
            return responseDto;
        }
        [HttpPost("RemoveCoupon")]
        public async Task<object> RemoveCoupon([FromBody] CartDto cartDto)
        {
            try
            {
                var cartFromDb = await _appDbContext.CartHeaders.FirstAsync(u => u.UserId == cartDto.CartHeader.UserId);
                cartFromDb.CouponCode = "";
                _appDbContext.CartHeaders.Update(cartFromDb);
                return await _appDbContext.SaveChangesAsync();
                responseDto.Result = true;
            }
            catch (Exception ex)
            {
                responseDto.IsSuccess = false;
                responseDto.Message = ex.Message.ToString();
            }
            return responseDto;
        }
    }
}
